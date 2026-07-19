using InstaSafe.Application.Chatbot.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace InstaSafe.Api.Controllers;

[ApiController]
[Route("api/chatbot")]
public class ChatbotController : ControllerBase
{
    private readonly ISender _sender;

    public ChatbotController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Receives incoming WhatsApp messages from OpenWA webhook.
    /// OpenWA payload: { "event": "message.received", "sessionId": "...", "data": { "from": "...@c.us", "body": "...", "type": "chat" } }
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] OpenWaWebhookPayload payload)
    {
        // Only process text messages
        if (payload.Event != "message.received" || payload.Data == null)
            return Ok();

        var messageType = payload.Data.Type;
        if ((messageType != "text" && messageType != "chat") || string.IsNullOrWhiteSpace(payload.Data.Body))
            return Ok();

        // OpenWA sends "from" as "2348xxxxxxxxx@c.us" — strip the @c.us suffix to get the phone number
        var phoneNumber = payload.Data.From?.Replace("@c.us", "") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(phoneNumber))
            return Ok();

        var command = new ProcessChatbotMessageCommand(phoneNumber, payload.Data.Body);
        await _sender.Send(command);

        return Ok();
    }
}

// OpenWA Webhook DTOs
public class OpenWaWebhookPayload
{
    [JsonPropertyName("event")]
    public string? Event { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("sessionId")]
    public string? SessionId { get; set; }

    [JsonPropertyName("data")]
    public OpenWaMessageData? Data { get; set; }
}

public class OpenWaMessageData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
