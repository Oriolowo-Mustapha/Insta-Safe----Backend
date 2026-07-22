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
                                  "If they want to create an order, extract the following into the 'ExtractedData' field as a JSON string with keys: 'Item' (string), 'Description' (string), 'Price' (number), 'Location' (string), 'BuyerPhone' (string), 'BuyerEmail' (string), and 'DispatcherPhone' (string). " +
                                  "Ensure 'BuyerPhone' and 'DispatcherPhone' are formatted with the international country code (e.g., +234). 'DispatcherPhone' can be empty if they don't have a rider yet. " +
                                  "If ANY of these required fields (Item, Description, Price, Location, BuyerPhone, BuyerEmail) are missing or unclear, provide a helpful 'ReplyMessage' asking the user for the specific missing details. If all required fields are present, leave 'ReplyMessage' empty. " +
                                  "If the intent is 'Unknown', provide a helpful 'ReplyMessage' asking them to clarify if they want to create an order or check a status. " +
                                  "Return STRICTLY JSON: { \"Intent\": string, \"ExtractedData\": string, \"ReplyMessage\": string }. Do not use markdown blocks."
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

    public async Task<DisputeAnalysisResult> AnalyzeDisputeAsync(string itemDescription, string buyerReason, string? evidenceUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var contentList = new List<object>
            {
                new { 
                    type = "text", 
                    text = $"You are an AI Dispute Resolution Assistant for InstaSafe. The merchant originally listed the item as: '{itemDescription}'. The buyer has now raised a dispute stating: '{buyerReason}'. Please analyze the provided image evidence against the merchant's description. Evaluate if the buyer's claim is valid. Return STRICTLY a JSON object with 'ConfidenceScore' (0-100 integer representing how confident you are that the buyer is correct) and 'Summary' (1-2 sentence explanation of your verdict)." 
                }
            };

            if (!string.IsNullOrEmpty(evidenceUrl))
            {
                contentList.Add(new
                {
                    type = "image_url",
                    image_url = new { url = evidenceUrl }
                });
            }

            var requestBody = new
            {
                model = "google/gemini-1.5-pro", // High accuracy vision model via OpenRouter
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = contentList
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
                return new DisputeAnalysisResult { ConfidenceScore = 0, Summary = "Failed to generate AI analysis." };
            }

            var result = JsonSerializer.Deserialize<DisputeAnalysisResult>(contentString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? new DisputeAnalysisResult { ConfidenceScore = 0, Summary = "Failed to parse AI response." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze dispute via OpenRouter Vision API.");
            return new DisputeAnalysisResult { ConfidenceScore = 0, Summary = "AI Vision analysis is currently unavailable." };
        }
    }
}
