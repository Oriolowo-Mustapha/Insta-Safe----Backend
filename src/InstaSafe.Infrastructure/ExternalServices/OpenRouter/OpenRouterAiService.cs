using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InstaSafe.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InstaSafe.Infrastructure.ExternalServices.OpenRouter;

public class OpenRouterAiService : IDisputeResolutionAiService
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouterOptions _options;
    private readonly ILogger<OpenRouterAiService> _logger;

    public OpenRouterAiService(HttpClient httpClient, IOptions<OpenRouterOptions> options, ILogger<OpenRouterAiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/chat/completions");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("HTTP-Referer", "https://instasafe.com");
        _httpClient.DefaultRequestHeaders.Add("X-Title", "InstaSafe");
    }

    public async Task<DisputeResolutionResult> AnalyzeDisputeEvidenceAsync(string itemDescription, List<string> evidenceUrls, CancellationToken cancellationToken = default)
    {
        try
        {
            var contentList = new List<object>
            {
                new
                {
                    type = "text",
                    text = $"You are an impartial dispute resolution AI for an escrow platform. " +
                           $"The buyer purchased an item described as: '{itemDescription}'. " +
                           $"The buyer has raised a dispute and provided the following evidence images. " +
                           $"Analyze the images to determine if the item received is significantly different from the description, damaged, or entirely incorrect. " +
                           $"If the buyer's claim is valid, suggest 'RefundBuyer'. If the evidence doesn't show a valid issue, suggest 'ReleaseToMerchant'. " +
                           $"Respond STRICTLY in JSON format with three fields: 'SuggestedResolution' (string: 'RefundBuyer' or 'ReleaseToMerchant'), 'ConfidenceScore' (integer 0-100), and 'Reasoning' (string: brief explanation)."
                }
            };

            foreach (var url in evidenceUrls)
            {
                contentList.Add(new
                {
                    type = "image_url",
                    image_url = new { url = url }
                });
            }

            var requestBody = new
            {
                model = _options.Model,
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
                throw new Exception("OpenRouter returned empty content.");
            }

            var result = JsonSerializer.Deserialize<DisputeResolutionResult>(contentString, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (result == null)
            {
                throw new Exception("Failed to deserialize OpenRouter response into DisputeResolutionResult.");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while calling OpenRouter API for dispute resolution.");
            // Default safe fallback if AI fails
            return new DisputeResolutionResult
            {
                SuggestedResolution = "ManualReview",
                ConfidenceScore = 0,
                Reasoning = $"AI Analysis failed: {ex.Message}. Requires manual human review."
            };
        }
    }
}
