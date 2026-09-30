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
        string email, long amountKobo, Guid orderId, CancellationToken ct,
        string? callbackUrl = null)
    {
        // callback_url is where Paystack sends the buyer after payment. Null
        // keeps Paystack's default success page; a track URL lands them on
        // their live tracker (which may briefly show AwaitingPayment until the
        // webhook confirms - the page already renders that state).
        var body = new { email, amount = amountKobo, metadata = new { order_id = orderId }, callback_url = callbackUrl };
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

    public async Task<(bool Success, string? CustomerCode, string? Error)> CreateCustomerAsync(
        string email, string firstName, string lastName, string phone, Guid orderId, CancellationToken ct)
    {
        var body = new
        {
            email,
            first_name = firstName,
            last_name = lastName,
            phone,
            metadata = new { order_id = orderId }
        };
        var res = await _http.PostAsJsonAsync("/customer", body, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("Customer creation failed: {Status} {Body}", res.StatusCode, raw);
            return (false, null, $"Paystack customer rejected ({(int)res.StatusCode}).");
        }
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var code = doc.RootElement.GetProperty("data").GetProperty("customer_code").GetString();
            return (true, code, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Customer response unparseable");
            return (false, null, "Paystack customer response unreadable.");
        }
    }

    public async Task<(bool Success, string? AccountNumber, string? AccountName, string? Bank, string? Error)> AssignDedicatedAccountAsync(
        string customerCode, string? preferredBank, CancellationToken ct)
    {
        object body = string.IsNullOrWhiteSpace(preferredBank)
            ? new { customer = customerCode }
            : new { customer = customerCode, preferred_bank = preferredBank };
        var res = await _http.PostAsJsonAsync("/dedicated_account", body, ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("DVA assign failed for {Customer}: {Status} {Body}", customerCode, res.StatusCode, raw);
            return (false, null, null, null, $"Virtual account rejected ({(int)res.StatusCode}).");
        }
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var data = doc.RootElement.GetProperty("data");
            var bank = data.TryGetProperty("bank", out var b) ? b.GetProperty("name").GetString() : null;
            return (true,
                data.TryGetProperty("account_number", out var a) ? a.GetString() : null,
                data.TryGetProperty("account_name", out var n) ? n.GetString() : null,
                bank, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DVA response unparseable for {Customer}", customerCode);
            return (false, null, null, null, "Virtual account response unreadable.");
        }
    }

    public async Task<List<(string Name, string Slug, string Code)>> ListTransferBanksAsync(CancellationToken ct)
    {
        var res = await _http.GetAsync("/bank?country=nigeria&pay_with_bank_transfer=true&perPage=100", ct);
        if (!res.IsSuccessStatusCode) return new();
        try
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            return doc.RootElement.GetProperty("data").EnumerateArray()
                .Select(b => (
                    Name: b.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    Slug: b.TryGetProperty("slug", out var s) ? s.GetString() ?? "" : "",
                    Code: b.TryGetProperty("code", out var c) ? c.GetString() ?? "" : ""))
                .Where(b => !string.IsNullOrEmpty(b.Name))
                .ToList();
        }
        catch
        {
            return new();
        }
    }

    public async Task<List<(string Name, string Slug, string Code)>> ListAllBanksAsync(CancellationToken ct)
    {
        // Full Nigerian bank list (payout dropdowns, WhatsApp bank matching).
        // Paystack ignores perPage as a cap here: one call returns everything
        // (~280 rows). Do NOT use cursor mode - its page sizes vary, and the
        // old "short page means last page" heuristic silently truncated the
        // list to ~98 banks by stopping after page 1. Dedupe by code as a
        // guard against repeated rows.
        var res = await _http.GetAsync("/bank?country=nigeria&perPage=100", ct);
        if (!res.IsSuccessStatusCode) return new();
        try
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var all = new List<(string Name, string Slug, string Code)>();
            foreach (var b in doc.RootElement.GetProperty("data").EnumerateArray())
            {
                var bank = (
                    Name: b.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    Slug: b.TryGetProperty("slug", out var s) ? s.GetString() ?? "" : "",
                    Code: b.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "");
                if (!string.IsNullOrEmpty(bank.Name) && !string.IsNullOrEmpty(bank.Code)
                    && !all.Any(x => x.Code == bank.Code))
                    all.Add(bank);
            }
            return all;
        }
        catch
        {
            return new();
        }
    }

    public async Task<AccountResolveResult> ResolveAccountAsync(
        string accountNumber, string bankCode, CancellationToken ct)
    {
        var res = await _http.GetAsync(
            $"/bank/resolve?account_number={Uri.EscapeDataString(accountNumber)}&bank_code={Uri.EscapeDataString(bankCode)}", ct);
        var raw = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("Account resolve failed: {Status} {Body}", res.StatusCode, raw);
            var paystackMsg = PaystackMessage(raw);
            if (IsServiceDown(res.StatusCode, paystackMsg))
                return new AccountResolveResult(false, null, ResolveFailureKind.Unavailable,
                    "Bank verification is temporarily unavailable. Please double-check the account number and bank yourself and continue — we'll retry verification later.");
            return new AccountResolveResult(false, null, ResolveFailureKind.Invalid,
                $"Could not verify this account ({paystackMsg}). Check the number and bank, then retry.");
        }
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var name = doc.RootElement.GetProperty("data").GetProperty("account_name").GetString();
            if (string.IsNullOrWhiteSpace(name))
                return new AccountResolveResult(false, null, ResolveFailureKind.Invalid,
                    "No account name returned. Check the number and bank, then retry.");
            return new AccountResolveResult(true, name, ResolveFailureKind.Invalid, "");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Account resolve response unparseable");
            return new AccountResolveResult(false, null, ResolveFailureKind.Unavailable,
                "Bank verification is temporarily unavailable. Please double-check the details yourself and continue.");
        }
    }

    private static bool IsServiceDown(System.Net.HttpStatusCode status, string message)
    {
        if ((int)status == 429 || (int)status >= 500) return true;
        var m = message.ToLowerInvariant();
        return m.Contains("limit") || m.Contains("exceed") || m.Contains("unavailable")
            || m.Contains("try again later") || m.Contains("timeout") || m.Contains("test bank codes");
    }

    private static string PaystackMessage(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.TryGetProperty("message", out var m)
                ? m.GetString() ?? "rejected"
                : "rejected";
        }
        catch
        {
            return "rejected";
        }
    }
}
