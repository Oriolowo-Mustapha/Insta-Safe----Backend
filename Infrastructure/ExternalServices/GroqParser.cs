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
        var schemaHint = """{"customer_name":"string","customer_phone":"string","address":"string","items":[{"description":"string","quantity":1,"unit_price_ngn":0}],"total_ngn":0,"delivery_fee_ngn":0,"driver_phone":"string or empty"}""";
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
            r.TryGetProperty("driver_phone", out var dp) ? dp.GetString() ?? "" : "");
    }

    public async Task<ChatIntent> ClassifyIntentAsync(string rawText, CancellationToken ct)
    {
        const string schemaHint = """{"intent":"greeting|menu_select|create_order|track_order|help|cancel|unknown","menu_option":1,"track_reference":"string or empty"}""";
        var body = new
        {
            model = _opts.Model,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = "You route WhatsApp messages for InstaSafe, a Nigerian escrow service. Reply with JSON only, matching " + schemaHint + ". Examples: 'hi' -> greeting. '1'/'2'/'3'/'4' -> menu_select with that menu_option (4 = continue an unfinished order). '2 sneakers for Chidi, 08012345678, Lekki, 45000' -> create_order. 'where is my order ref-123' -> track_order with track_reference 'ref-123'. 'help'/'support' -> help. 'cancel'/'stop' -> cancel. Anything else -> unknown." },
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
}
