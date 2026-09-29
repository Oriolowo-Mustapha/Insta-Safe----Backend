using InstaSafe.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InstaSafe.Infrastructure.ExternalServices;

public class GroqOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "openai/gpt-oss-120b";
}

public class GroqParser : IGroqParser
{
    private readonly HttpClient _http;
    private readonly GroqOptions _opts;

    public GroqParser(HttpClient http, IConfiguration config)
    {
        _http = http;
        _opts = new GroqOptions
        {
            ApiKey = config["Groq:ApiKey"] ?? string.Empty,
            Model = config["Groq:Model"] ?? "openai/gpt-oss-120b"
        };
        _http.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
        if (!string.IsNullOrEmpty(_opts.ApiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);
    }

    public async Task<ParsedOrder> ParseOrderTextAsync(string rawText, CancellationToken ct)
    {
        var schemaHint = """{"customer_name":"string","customer_phone":"string","buyer_email":"string or empty","address":"string","items":[{"description":"string","quantity":1,"unit_price_ngn":0}],"total_ngn":0,"delivery_fee_ngn":0,"driver_phone":"string or empty","fulfillment":"dispatch, digital, or empty when unclear"}""";
        var body = new
        {
            model = _opts.Model,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = $"Extract a Nigerian social-commerce order into JSON only, matching {schemaHint}." },
                new { role = "user", content = rawText }
            },
            temperature = 0
        };
        var res = await _http.PostAsJsonAsync("chat/completions", body, ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()!;
        using var order = JsonDocument.Parse(content);
        var r = order.RootElement;
        var items = r.GetProperty("items").EnumerateArray()
            .Select(i => new ParsedItem(
                i.GetProperty("description").GetString() ?? "Item",
                i.TryGetProperty("quantity", out var q) ? q.GetInt32() : 1,
                i.TryGetProperty("unit_price_ngn", out var p) ? p.GetInt64() : 0)).ToList();
        return new ParsedOrder(
            r.GetProperty("customer_name").GetString() ?? "",
            r.GetProperty("customer_phone").GetString() ?? "",
            r.GetProperty("address").GetString() ?? "",
            items,
            r.TryGetProperty("total_ngn", out var t) ? t.GetInt64() : 0,
            r.TryGetProperty("delivery_fee_ngn", out var f) ? f.GetInt64() : 0,
            r.TryGetProperty("driver_phone", out var dp) ? dp.GetString() ?? "" : "",
            r.TryGetProperty("buyer_email", out var be) ? be.GetString() ?? "" : "",
            r.TryGetProperty("fulfillment", out var fh) ? fh.GetString() ?? "" : "");
    }

    public async Task<ChatIntent> ClassifyIntentAsync(string rawText, CancellationToken ct)
    {
        const string schemaHint = """{"intent":"greeting|menu_select|create_order|track_order|list_orders|chitchat|help|cancel|unknown","menu_option":1,"track_reference":"string or empty"}""";
        var body = new
        {
            model = _opts.Model,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = "You route WhatsApp messages for InstaSafe, a Nigerian escrow service. Reply with JSON only, matching " + schemaHint + ". Examples: 'hi'/'hello'/'good morning' -> greeting. '1'/'2'/'3'/'4' -> menu_select with that menu_option (4 = continue an unfinished order). '2 sneakers for Chidi, 08012345678, Lekki, 45000' -> create_order. 'where is my order ref-123'/'track IS-8K4N2Q' -> track_order with track_reference. 'list all my orders'/'show my orders'/'i dunno the reference just list my orders' -> list_orders. 'thanks'/'thank you'/'lol'/'how far'/'wetin dey'/'abeg'/'good evening o' -> chitchat. 'help'/'support'/'i need help' -> help. 'cancel'/'stop' -> cancel. Anything else -> unknown." },
                new { role = "user", content = rawText }
            },
            temperature = 0
        };
        ChatIntent fallback() => new(ChatIntentKind.Unknown, null, null);
        HttpResponseMessage res;
        try
        {
            res = await _http.PostAsJsonAsync("chat/completions", body, ct);
        }
        catch
        {
            return fallback();
        }
        if (!res.IsSuccessStatusCode) return fallback();
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        }
        catch
        {
            return fallback();
        }
        using (doc)
        {
            try
            {
                var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                using var parsed = JsonDocument.Parse(content);
                var r = parsed.RootElement;
                var intent = r.TryGetProperty("intent", out var iv) ? iv.GetString() ?? "" : "";
                var kind = intent switch
                {
                    "greeting" => ChatIntentKind.Greeting,
                    "menu_select" => ChatIntentKind.MenuSelect,
                    "create_order" => ChatIntentKind.CreateOrder,
                    "track_order" => ChatIntentKind.TrackOrder,
                    "list_orders" => ChatIntentKind.ListOrders,
                    "chitchat" => ChatIntentKind.Chitchat,
                    "help" => ChatIntentKind.Help,
                    "cancel" => ChatIntentKind.Cancel,
                    _ => ChatIntentKind.Unknown
                };
                int? option = null;
                if (r.TryGetProperty("menu_option", out var ov) && ov.TryGetInt32(out var o)) option = o;
                var trackRef = r.TryGetProperty("track_reference", out var tv) ? tv.GetString() : null;
                if (string.IsNullOrWhiteSpace(trackRef)) trackRef = null;
                return new ChatIntent(kind, option, trackRef);
            }
            catch
            {
                return fallback();
            }
        }
    }

    public async Task<CorrectionInterpretation> InterpretCorrectionAsync(
        string rawText, string currentQuestion, string filledSummary, CancellationToken ct)
    {
        const string schemaHint = """{"is_correction":true,"field":"customer_name|customer_phone|buyer_email|address|amount|delivery_fee|driver_phone|driver_account|none","value":"string or empty"}""";
        var body = new
        {
            model = _opts.Model,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content =
                    "You help a Nigerian vendor build an escrow order over WhatsApp. " +
                    "The vendor is currently being asked: '" + currentQuestion + "'. " +
                    "Answers collected so far: " + filledSummary + ". " +
                    "The vendor just replied with a message. Decide ONLY this: is the reply " +
                    "correcting something given earlier (is_correction=true), or is it answering " +
                    "the current question / chitchat / anything else (is_correction=false)? " +
                    "Reply with JSON only, matching " + schemaHint + ". " +
                    "Rules: field must be one of customer_name, customer_phone, buyer_email, " +
                    "address, amount, delivery_fee, driver_phone, driver_account, or none. " +
                    "value is the corrected value exactly as stated - just the value, no commentary. " +
                    "NEVER invent a value; if the reply names a field but gives no usable value, " +
                    "is_correction=false. Bank names, greetings, thanks, questions, and answers " +
                    "to the current question are NEVER corrections. " +
                    "Examples: 'the address is actually 14 Allen Avenue Ikeja' while asked for a " +
                    "phone number -> {\"is_correction\":true,\"field\":\"address\",\"value\":\"14 Allen Avenue Ikeja\"}. " +
                    "'sorry I meant chidi@gmail.com' -> {\"is_correction\":true,\"field\":\"buyer_email\",\"value\":\"chidi@gmail.com\"}. " +
                    "'08031234567' when asked for a phone -> is_correction=false. " +
                    "'how far'/'thanks' -> is_correction=false." },
                new { role = "user", content = rawText }
            },
            temperature = 0
        };
        CorrectionInterpretation fallback() => new(false, null, null);
        HttpResponseMessage res;
        try
        {
            res = await _http.PostAsJsonAsync("chat/completions", body, ct);
        }
        catch
        {
            return fallback();
        }
        if (!res.IsSuccessStatusCode) return fallback();
        try
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var content = doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString() ?? "";
            using var parsed = JsonDocument.Parse(content);
            var r = parsed.RootElement;
            var isCorrection = r.TryGetProperty("is_correction", out var ic)
                && ic.ValueKind == JsonValueKind.True;
            var field = r.TryGetProperty("field", out var f) ? f.GetString() : null;
            var value = r.TryGetProperty("value", out var v) ? v.GetString() : null;
            if (!isCorrection) return fallback();
            field = (field ?? "").Trim().ToLowerInvariant();
            var allowed = new HashSet<string>(StringComparer.Ordinal)
            {
                "customer_name", "customer_phone", "buyer_email", "address",
                "amount", "delivery_fee", "driver_phone", "driver_account"
            };
            if (!allowed.Contains(field) || string.IsNullOrWhiteSpace(value))
                return fallback();
            return new CorrectionInterpretation(true, field, value.Trim());
        }
        catch
        {
            return fallback();
        }
    }

    public async Task<string> ChatReplyAsync(string rawText, CancellationToken ct)
    {
        var body = new
        {
            model = _opts.Model,
            messages = new object[]
            {
                new { role = "system", content = "You are InstaSafe's friendly WhatsApp assistant for Nigerian vendors (escrow for social commerce). Reply in 1-2 short sentences, warm Nigerian English, light pidgin OK when the user uses it. NEVER claim any action was taken, never invent order details, amounts, or links. For anything about orders, money, or help, end with: type MENU to see options." },
                new { role = "user", content = rawText }
            },
            temperature = 0.7,
            max_tokens = 120
        };
        try
        {
            var res = await _http.PostAsJsonAsync("chat/completions", body, ct);
            if (!res.IsSuccessStatusCode) return "";
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            return doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
        }
        catch
        {
            return "";
        }
    }
}
