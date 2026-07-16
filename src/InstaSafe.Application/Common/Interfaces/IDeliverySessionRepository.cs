using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Common.Interfaces;

public interface IDeliverySessionRepository
{
    Task<DeliverySession?> GetBySessionIdAsync(Guid sessionId, CancellationToken ct);
    void Add(DeliverySession session);
}
