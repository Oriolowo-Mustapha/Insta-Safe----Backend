using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.Commands.CreateOrder;
using InstaSafe.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace InstaSafe.Application.Features.Webhooks.Commands.ProcessOpenWAWebhook;

public class ProcessOpenWAWebhookCommandHandler : IRequestHandler<ProcessOpenWAWebhookCommand, Result<bool>>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IGroqParser _parser;
    private readonly IWhatsAppSender _sender;
    private readonly IConfiguration _config;
    private readonly ILogger<ProcessOpenWAWebhookCommandHandler> _logger;
    private readonly ConversationRouter _router;

    public ProcessOpenWAWebhookCommandHandler(
        IAppDbContext db, IMediator mediator, IGroqParser parser,
        IWhatsAppSender sender, IConfiguration config,
        ILogger<ProcessOpenWAWebhookCommandHandler> logger,
        ConversationRouter router)
    {
        _db = db; _mediator = mediator; _parser = parser;
        _sender = sender; _config = config; _logger = logger;
        _router = router;
    }

    public async Task<Result<bool>> Handle(ProcessOpenWAWebhookCommand req, CancellationToken ct)
    {
        var secret = _config["OpenWA:WebhookSecret"] ?? "";
        if (!VerifySignature(req.RawBody, req.Signature, secret))
            throw new UnauthorizedAccessException("Invalid webhook signature.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(req.RawBody);
        }
        catch
        {
            return Result<bool>.Failure("Malformed webhook payload.");
        }

        using (doc)
        {
            var root = doc.RootElement;
            var evt = root.TryGetProperty("event", out var e) ? e.GetString() ?? "unknown" : "unknown";
            var idempotencyKey = root.TryGetProperty("idempotencyKey", out var k) ? k.GetString() : null;

            // At-least-once dedupe: retries and engine re-fires carry the same key.
            if (!string.IsNullOrEmpty(idempotencyKey) &&
                await _db.WebhookEvents.AnyAsync(w => w.IdempotencyKey == idempotencyKey, ct))
            {
                return Result<bool>.Success(false);
            }

            var record = new WebhookEvent
            {
                Provider = "openwa",
                EventType = evt,
                Payload = req.RawBody.Length > 8000
                    ? Encoding.UTF8.GetString(req.RawBody, 0, 8000)
                    : Encoding.UTF8.GetString(req.RawBody),
                SignatureValid = true,
                Processed = false,
                IdempotencyKey = idempotencyKey
            };
            _db.WebhookEvents.Add(record);
            await _db.SaveChangesAsync(ct);

            try
            {
                var handled = false;
                if (evt == "message.received")
                    handled = await HandleInboundMessageAsync(root, ct);
                record.Processed = true;
                await _db.SaveChangesAsync(ct);
                return Result<bool>.Success(handled);
            }
            catch (Exception ex)
            {
                // Swallow: OpenWA must get 2xx once accepted; failure stays in the log + unprocessed row.
                _logger.LogError(ex, "OpenWA inbound handling failed");
                return Result<bool>.Success(false);
            }
        }
    }

    private async Task<bool> HandleInboundMessageAsync(JsonElement root, CancellationToken ct)
    {
        if (!root.TryGetProperty("data", out var data)) return false;
        if (data.TryGetProperty("type", out var t) && t.GetString() != "text") return false;
        if (data.TryGetProperty("fromMe", out var fm) && fm.GetBoolean()) return false;

        var from = data.TryGetProperty("from", out var f) ? f.GetString() ?? "" : "";
        var body = data.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(body)) return false;

        var vendorPhone = from.Contains('@') ? from[..from.IndexOf('@')] : from;

        // Admin audit trail: persisted immediately so it survives downstream failures.
        _db.ChatMessages.Add(new ChatMessage
        {
            Phone = vendorPhone,
            Direction = ChatDirection.Inbound,
            Body = body.Length > 1000 ? body[..1000] : body
        });
        await _db.SaveChangesAsync(ct);

        return await _router.RouteAsync(vendorPhone, body, from, ct);
    }

    internal static bool VerifySignature(byte[] rawBody, string signature, string secret)
    {
        if (string.IsNullOrEmpty(secret)) return true; // local/dev convenience
        if (string.IsNullOrEmpty(signature)) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = "sha256=" + Convert.ToHexString(hmac.ComputeHash(rawBody)).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
    }
}
