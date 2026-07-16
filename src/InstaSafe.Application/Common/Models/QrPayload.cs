namespace InstaSafe.Application.Common.Models;

public record QrPayload(
    Guid OrderId,
    Guid ActorId,
    string Nonce,
    DateTime IssuedAt,
    DateTime ExpiresAt
);
