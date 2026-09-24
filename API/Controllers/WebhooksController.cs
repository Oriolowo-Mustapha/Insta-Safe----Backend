using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Features.Orders.Commands.MarkFundsHeld;
using InstaSafe.Application.Features.Webhooks.Commands.ProcessOpenWAWebhook;
using InstaSafe.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
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
        var evt = doc.RootElement.GetProperty("event").GetString();
        if (evt == "charge.success")
        {
            var reference = doc.RootElement.GetProperty("data").GetProperty("reference").GetString()!;
            await _mediator.Send(new MarkFundsHeldCommand(reference), ct);
        }
        return Ok();
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
