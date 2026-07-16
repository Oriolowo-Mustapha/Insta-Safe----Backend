using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Delivery.Commands.CreatePickupSession;

public record CreatePickupSessionCommand(
    Guid OrderId,
    string MerchantQrToken,
    string DeviceFingerprint,
    double? Latitude,
    double? Longitude
) : IRequest<Result<CreatePickupSessionResponse>>;

public record CreatePickupSessionResponse(Guid SessionId, string Status, DateTime ExpiresAt);
