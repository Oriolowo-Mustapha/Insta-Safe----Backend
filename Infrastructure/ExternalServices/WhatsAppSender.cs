using InstaSafe.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace InstaSafe.Infrastructure.ExternalServices;

public class OpenWAOptions
{
    public string BaseUrl { get; set; } = "http://localhost:2785";
    public string ApiKey { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}

/// <summary>
/// Sends WhatsApp messages through a self-hosted OpenWA gateway.
/// All credentials come from the <c>OpenWA</c> appsettings section.
/// </summary>
public class WhatsAppSender : IWhatsAppSender
{
    private readonly HttpClient _http;
    private readonly OpenWAOptions _opts;
    private readonly ILogger<WhatsAppSender> _logger;

    public WhatsAppSender(HttpClient http, IConfiguration config, ILogger<WhatsAppSender> logger)
    {
        _http = http;
        _logger = logger;
        _opts = new OpenWAOptions
        {
            BaseUrl = (config["OpenWA:BaseUrl"] ?? "http://localhost:2785").TrimEnd('/'),
            ApiKey = config["OpenWA:ApiKey"] ?? string.Empty,
            SessionId = config["OpenWA:SessionId"] ?? string.Empty,
            WebhookSecret = config["OpenWA:WebhookSecret"] ?? string.Empty
        };
    }

    public Task SendTextAsync(string toPhone, string body, CancellationToken ct) =>
        SendAsync("send-text", new Dictionary<string, object?>
        {
            ["chatId"] = ToJid(toPhone),
            ["text"] = body
        }, toPhone, body, ct);

    public Task SendTemplateAsync(string toPhone, string templateName, Dictionary<string, string> vars, CancellationToken ct) =>
        SendAsync("send-template", new Dictionary<string, object?>
        {
            ["chatId"] = ToJid(toPhone),
            ["templateName"] = templateName,
            ["vars"] = vars
        }, toPhone, $"template:{templateName}", ct);

    private async Task SendAsync(string route, Dictionary<string, object?> payload, string toPhone, string logPreview, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_opts.ApiKey) || string.IsNullOrEmpty(_opts.SessionId))
        {
            _logger.LogInformation("[OpenWA:mock] To {To}: {Preview}", toPhone, logPreview);
            return;
        }

        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_opts.BaseUrl}/api/sessions/{_opts.SessionId}/messages/{route}");
        req.Headers.Add("X-API-Key", _opts.ApiKey);
        req.Content = JsonContent.Create(payload);
        var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            _logger.LogError("OpenWA send-text failed: {Status} {Body} (to {To})",
                (int)res.StatusCode, body, ToJid(toPhone));
            res.EnsureSuccessStatusCode();
        }
    }

    /// <summary>
    /// Normalizes Nigerian/international numbers to an OpenWA person JID.
    /// Accepts "080...", "+234...", "234...", or an existing "...@c.us" JID.
    /// </summary>
    internal static string ToJid(string phone)
    {
        if (phone.Contains('@')) return phone.Trim();
        var digits = Regex.Replace(phone, @"\D", "");
        if (digits.StartsWith("0")) digits = "234" + digits[1..];
        return digits + "@c.us";
    }
}
