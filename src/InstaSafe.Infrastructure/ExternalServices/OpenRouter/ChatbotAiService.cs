using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InstaSafe.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InstaSafe.Infrastructure.ExternalServices.OpenRouter;

public class ChatbotAiService : IChatbotAiService
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouterOptions _options;
    private readonly ILogger<ChatbotAiService> _logger;

    public ChatbotAiService(HttpClient httpClient, IOptions<OpenRouterOptions> options, ILogger<ChatbotAiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/chat/completions");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://instasafe.com");
        _httpClient.DefaultRequestHeaders.Add("X-Title", "InstaSafe Chatbot");
    }

    public async Task<ChatbotIntentResult> ParseIntentAsync(string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new
            {
                model = _options.Model,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an NLP intent parser for the InstaSafe WhatsApp chatbot. " +
                                  "Users will send messages to either create a new escrow order, check the status of an existing order, or ask general questions. " +
                                  "Extract the intent as 'CreateOrder', 'CheckStatus', or 'Unknown'. " +
                                  "If they want to create an order, extract the following into the 'ExtractedData' field as a JSON string with keys: 'Item' (string), 'Price' (number), and 'Location' (string). " +
                                  "If ANY of these 3 fields are missing or unclear, provide a helpful 'ReplyMessage' asking the user for the missing details. If all 3 are present, leave 'ReplyMessage' empty. " +
                                  "If the intent is 'Unknown', provide a helpful 'ReplyMessage' asking them to clarify if they want to create an order or check a status. " +
                                  "Return STRICTLY JSON: { \"Intent\": string, \"ExtractedData\": string, \"ReplyMessage\": string }."
                    },
                    new
                    {
                        role = "user",
                        content = message
                    }
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("", jsonContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(responseString);
            var contentString = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            if (contentString == null)
            {
                return new ChatbotIntentResult { Intent = "Unknown", ReplyMessage = "I'm having trouble understanding right now. Do you want to create an order or check an order status?" };
            }

            var result = JsonSerializer.Deserialize<ChatbotIntentResult>(contentString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? new ChatbotIntentResult { Intent = "Unknown" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse chatbot intent via OpenRouter.");
            return new ChatbotIntentResult { Intent = "Unknown", ReplyMessage = "Sorry, our AI service is currently down." };
        }
    }
}
