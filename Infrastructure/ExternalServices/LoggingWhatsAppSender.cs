using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Infrastructure.ExternalServices;

/// <summary>
/// Persists every outbound WhatsApp message to the admin audit trail.
/// Never breaks delivery: logging failures are swallowed.
/// </summary>
public class LoggingWhatsAppSender : IWhatsAppSender
{
    private readonly IWhatsAppSender _inner;
    private readonly IAppDbContext _db;
    private readonly ILogger<LoggingWhatsAppSender> _logger;

    public LoggingWhatsAppSender(IWhatsAppSender inner, IAppDbContext db, ILogger<LoggingWhatsAppSender> logger)
    {
        _inner = inner; _db = db; _logger = logger;
    }

    public async Task SendTextAsync(string toPhone, string body, CancellationToken ct)
    {
        await _inner.SendTextAsync(toPhone, body, ct);
        await LogAsync(toPhone, body);
    }

    public async Task SendTemplateAsync(string toPhone, string templateName, Dictionary<string, string> vars, CancellationToken ct)
    {
        await _inner.SendTemplateAsync(toPhone, templateName, vars, ct);
        await LogAsync(toPhone, $"template:{templateName}");
    }

    private async Task LogAsync(string toPhone, string body)
    {
        try
        {
            _db.ChatMessages.Add(new ChatMessage
            {
                Phone = ChatAudit.Normalize(toPhone),
                Direction = ChatDirection.Outbound,
                Body = ChatAudit.Truncate(body)
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat audit log failed");
        }
    }
}
