using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Delivery.Queries.GetDeliverySessionStatus;

public record GetDeliverySessionStatusQuery(Guid SessionId) : IRequest<Result<DeliverySessionStatusResponse>>;

public class DeliverySessionStatusResponse
{
    public Guid SessionId { get; init; }
    public Guid OrderId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? PickupTimestamp { get; init; }
    public DateTime? DeliveryTimestamp { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? FailureReason { get; init; }
}
