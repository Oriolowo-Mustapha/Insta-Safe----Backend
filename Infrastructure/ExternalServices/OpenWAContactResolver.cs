using InstaSafe.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace InstaSafe.Infrastructure.ExternalServices;

public class OpenWAContactResolver : IContactResolver
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _sessionId;
    private readonly ILogger<OpenWAContactResolver> _logger;

    public OpenWAContactResolver(HttpClient http, IConfiguration config, ILogger<OpenWAContactResolver> logger)
    {
        _http = http;
        _logger = logger;
        _baseUrl = (config["OpenWA:BaseUrl"] ?? "http://localhost:2785").TrimEnd('/');
        _apiKey = config["OpenWA:ApiKey"] ?? string.Empty;
        _sessionId = config["OpenWA:SessionId"] ?? string.Empty;
        _http.BaseAddress = new Uri(_baseUrl);
        if (!string.IsNullOrEmpty(_apiKey))
            _http.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
    }

    public async Task<string?> ResolvePhoneAsync(string contactJid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_sessionId))
            return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"/api/sessions/{_sessionId}/contacts/{Uri.EscapeDataString(contactJid)}/phone");
            var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("phone", out var p))
                return string.IsNullOrWhiteSpace(p.GetString()) ? null : p.GetString();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LID resolve failed for {Jid}", contactJid);
            return null;
        }
    }
}
