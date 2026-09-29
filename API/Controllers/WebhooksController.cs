using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Features.Orders.Commands.MarkFundsHeld;
using InstaSafe.Application.Features.Webhooks.Commands.ProcessOpenWAWebhook;
using InstaSafe.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(IMediator mediator, IConfiguration config, ILogger<WebhooksController> logger)
    {
        _mediator = mediator; _config = config; _logger = logger;
    }

    // OpenWA delivery: POST /webhooks/openwa
    // Register this URL at POST /api/sessions/{sessionId}/webhooks with events ["message.received"].
    // Always answers 2xx once accepted so OpenWA stops retrying; 401 only on bad signature.
    [HttpPost("openwa")]
    public async Task<IActionResult> ReceiveOpenWA(CancellationToken ct)
    {
        byte[] raw;
        using (var ms = new MemoryStream())
        {
            await Request.Body.CopyToAsync(ms, ct);
            raw = ms.ToArray();
        }

        var signature = Request.Headers["X-OpenWA-Signature"].FirstOrDefault() ?? "";
        await _mediator.Send(new ProcessOpenWAWebhookCommand(raw, signature), ct);
        return Ok();
    }

    // Paystack: POST /webhooks/paystack (x-paystack-signature = HMAC SHA512 of body)
    [HttpPost("paystack")]
    public async Task<IActionResult> ReceivePaystack(CancellationToken ct)
    {
        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            body = await reader.ReadToEndAsync(ct);

        var signature = Request.Headers["x-paystack-signature"].FirstOrDefault() ?? "";
        var secret = _config["Paystack:SecretKey"] ?? "";
        var valid = VerifyPaystackSignature(body, signature, secret);

        using var scope = HttpContext.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        db.WebhookEvents.Add(new WebhookEvent
        {
            Provider = "paystack",
            EventType = TryGetEvent(body),
            Payload = body.Length > 8000 ? body[..8000] : body,
            SignatureValid = valid
        });
        await db.SaveChangesAsync(ct);

        if (!valid) return Unauthorized();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var evt = root.GetProperty("event").GetString();
        if (evt == "charge.success")
        {
            var data = root.GetProperty("data");
            var reference = data.GetProperty("reference").GetString()!;
            var (customerEmail, amountKobo) = ExtractCustomer(data);
            await _mediator.Send(new MarkFundsHeldCommand(reference, customerEmail, amountKobo), ct);
        }
        else if (evt == "dedicatedaccount.assign.success")
        {
            await HandleDedicatedAssignAsync(root.GetProperty("data"), true, ct);
        }
        else if (evt is "dedicatedaccount.assign.failed" or "customeridentification.failed")
        {
            await HandleDedicatedAssignAsync(root.GetProperty("data"), false, ct);
        }
        return Ok();
    }

    private static (string? Email, long? AmountKobo) ExtractCustomer(JsonElement data)
    {
        try
        {
            string? email = null;
            if (data.TryGetProperty("customer", out var c) && c.ValueKind == JsonValueKind.Object
                && c.TryGetProperty("email", out var e))
                email = e.GetString();
            long? amount = data.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Number
                ? a.GetInt64() : null;
            return (email, amount);
        }
        catch
        {
            return (null, null);
        }
    }

    private async Task HandleDedicatedAssignAsync(JsonElement data, bool success, CancellationToken ct)
    {
        try
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            string? customerCode = null;
            if (data.TryGetProperty("customer", out var c) && c.ValueKind == JsonValueKind.Object
                && c.TryGetProperty("customer_code", out var cc))
                customerCode = cc.GetString();

            InstaSafe.Domain.Entities.Order? order = null;
            if (!string.IsNullOrWhiteSpace(customerCode))
                order = await db.Orders.FirstOrDefaultAsync(
                    o => o.PaystackCustomerCode == customerCode, ct);
            if (order is null) return;

            if (success)
            {
                string? number = null, name = null, bank = null;
                if (data.TryGetProperty("dedicated_account", out var d) && d.ValueKind == JsonValueKind.Object)
                {
                    if (d.TryGetProperty("account_number", out var a)) number = a.GetString();
                    if (d.TryGetProperty("account_name", out var n)) name = n.GetString();
                    if (d.TryGetProperty("bank", out var b) && b.ValueKind == JsonValueKind.Object
                        && b.TryGetProperty("name", out var bn)) bank = bn.GetString();
                }
                if (!string.IsNullOrWhiteSpace(number))
                {
                    order.PayVirtualAccountNumber = number;
                    order.PayVirtualAccountName = name;
                    order.PayVirtualAccountBank = bank;
                    order.Touch();
                    await db.SaveChangesAsync(ct);
                    var notifier = scope.ServiceProvider
                        .GetRequiredService<InstaSafe.Application.Common.Notifications.OrderNotifier>();
                    await notifier.BankTransferDetailsAsync(order);
                }
            }
            else
            {
                _logger.LogWarning("Dedicated account setup failed for order {OrderId}", order.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dedicated account webhook handling failed");
        }
    }

    private static string TryGetEvent(string body)
    {
        try { return JsonDocument.Parse(body).RootElement.GetProperty("event").GetString() ?? "unknown"; }
        catch { return "unknown"; }
    }

    private static bool VerifyPaystackSignature(string body, string signature, string secret)
    {
        if (string.IsNullOrEmpty(secret)) return true; // local/dev convenience
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hash), Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
    }
}
