using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Features.Webhooks.Commands.ProcessOpenWAWebhook;

/// <summary>
/// Processes one OpenWA delivery: verifies the HMAC signature, dedupes
/// retried deliveries, records the event, and dispatches inbound vendor
/// messages into the order flow. Returns true when a message was handled.
/// </summary>
public sealed record ProcessOpenWAWebhookCommand(byte[] RawBody, string Signature) : IRequest<Result<bool>>;
