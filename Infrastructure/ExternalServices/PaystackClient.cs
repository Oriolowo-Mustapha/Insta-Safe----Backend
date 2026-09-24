using InstaSafe.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InstaSafe.Infrastructure.ExternalServices;

public class PaystackOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.paystack.co";
}

public class PaystackClient : IPaystackClient
{
    private readonly HttpClient _http;
    private readonly PaystackOptions _opts;
    private readonly ILogger<PaystackClient> _logger;

    public PaystackClient(HttpClient http, IConfiguration config, ILogger<PaystackClient> logger)
    {
        _http = http;
        _logger = logger;
        _opts = new PaystackOptions
        {
            SecretKey = config["Paystack:SecretKey"] ?? string.Empty,
            BaseUrl = config["Paystack:BaseUrl"] ?? "https://api.paystack.co"
        };
        _http.BaseAddress = new Uri(_opts.BaseUrl);
        if (!string.IsNullOrEmpty(_opts.SecretKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opts.SecretKey);
    }

    public async Task<(string Reference, string AuthUrl)> InitializeTransactionAsync(
        string email, long amountKobo, Guid orderId, CancellationToken ct)
    {
        var body = new { email, amount = amountKobo, metadata = new { order_id = orderId }, callback_url = (string?)null };
        var res = await _http.PostAsJsonAsync("/transaction/initialize", body, ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var data = doc.RootElement.GetProperty("data");
        return (data.GetProperty("reference").GetString()!, data.GetProperty("authorization_url").GetString()!);
    }

    public async Task<bool> VerifyTransactionAsync(string reference, CancellationToken ct)
    {
        var res = await _http.GetAsync($"/transaction/verify/{reference}", ct);
        if (!res.IsSuccessStatusCode) return false;
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("data").GetProperty("status").GetString() == "success";
    }

    public async Task<string?> CreateRecipientAsync(string accountNumber, string bankCode, string name, CancellationToken ct)
    {
        var body = new { type = "nuban", name, account_number = accountNumber, bank_code = bankCode, currency = "NGN" };
        var res = await _http.PostAsJsonAsync("/transferrecipient", body, ct);
        if (!res.IsSuccessStatusCode) { _logger.LogWarning("Recipient creation failed: {Status}", res.StatusCode); return null; }
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("data").GetProperty("recipient_code").GetString();
    }

    public async Task<string?> InitiateTransferAsync(long amountKobo, string recipientCode, string reason, CancellationToken ct)
    {
        var body = new { source = "balance", amount = amountKobo, recipient = recipientCode, reason };
        var res = await _http.PostAsJsonAsync("/transfer", body, ct);
        if (!res.IsSuccessStatusCode) { _logger.LogWarning("Transfer failed: {Status}", res.StatusCode); return null; }
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("data").GetProperty("transfer_code").GetString();
    }

    public async Task<(bool Success, string? RefundReference, string? Error)> RefundTransactionAsync(string reference, CancellationToken ct)
    {
        var res = await _http.PostAsJsonAsync("/refund", new { transaction = reference }, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("Refund failed for {Reference}: {Status} {Body}", reference, res.StatusCode, raw);
            return (false, null, $"Paystack refund rejected ({(int)res.StatusCode}).");
        }
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var data = doc.RootElement.GetProperty("data");
            var refundRef = data.TryGetProperty("id", out var id)
                ? id.GetRawText().Trim('"')
                : data.TryGetProperty("reference", out var r) ? r.GetString() : null;
            return (true, refundRef, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Refund response unparseable for {Reference}", reference);
            return (false, null, "Paystack refund response unreadable.");
        }
    }
}
