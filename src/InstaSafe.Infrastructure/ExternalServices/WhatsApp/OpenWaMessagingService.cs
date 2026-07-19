using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using InstaSafe.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InstaSafe.Infrastructure.ExternalServices.WhatsApp;

public class OpenWaMessagingService : IWhatsAppMessagingService
{
    private readonly HttpClient _httpClient;
    private readonly OpenWaOptions _options;
    private readonly ILogger<OpenWaMessagingService> _logger;

    public OpenWaMessagingService(HttpClient httpClient, IOptions<OpenWaOptions> options, ILogger<OpenWaMessagingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            // OpenWA expects chatId in format: <number>@c.us
            var chatId = toPhoneNumber.TrimStart('+') + "@c.us";

            var requestBody = new
            {
                chatId,
                text = message
            };

            var url = $"{_options.BaseUrl.TrimEnd('/')}/sessions/{_options.SessionId}/messages/send-text";

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("X-API-Key", _options.ApiKey);
            request.Content = jsonContent;

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to send WhatsApp message via OpenWA. Status: {StatusCode}, Error: {Error}", response.StatusCode, errorContent);
            }
            else
            {
                _logger.LogInformation("WhatsApp message sent to {ChatId} via OpenWA.", chatId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while sending WhatsApp message via OpenWA.");
        }
    }
}
