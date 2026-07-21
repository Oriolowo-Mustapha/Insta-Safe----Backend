using InstaSafe.Application.Chatbot.Commands;
using InstaSafe.Application.Chatbot.Queries.GetChatbotSession;
using InstaSafe.Application.Chatbot.Queries.GetChatbotSessions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
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

        var messageType = payload.Data.Type ?? "chat";
        if (messageType != "text" && messageType != "chat" && messageType != "image")
            return Ok();

        // OpenWA sends "from" as "2348xxxxxxxxx@c.us" — strip the @c.us suffix to get the phone number
        var phoneNumber = payload.Data.From?.Replace("@c.us", "") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(phoneNumber))
            return Ok();

        var body = payload.Data.Body ?? "";

        var command = new ProcessChatbotMessageCommand(phoneNumber, body, messageType);
        await _sender.Send(command);

        return Ok();
    }

    /// <summary>Get all chatbot sessions (admin monitoring).</summary>
    [HttpGet("sessions")]
    [Authorize]
    public async Task<IActionResult> GetSessions()
    {
        var query = new GetChatbotSessionsQuery();
        var result = await _sender.Send(query);
        return result.Succeeded
            ? Ok(result.Data)
            : BadRequest(new { errors = result.Errors });
    }

    /// <summary>Get a specific chatbot session by ID.</summary>
    [HttpGet("sessions/{sessionId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetSession(Guid sessionId)
    {
        var query = new GetChatbotSessionQuery(sessionId);
        var result = await _sender.Send(query);
        return result.Succeeded
            ? Ok(result.Data)
            : NotFound(new { errors = result.Errors });
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
