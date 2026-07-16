using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Delivery.Commands.ConfirmDeliveryScan;

public record ConfirmDeliveryScanCommand(
    Guid OrderId,
    Guid SessionId,
    string BuyerQrToken,
    string DeviceFingerprint,
    double? Latitude,
    double? Longitude
) : IRequest<Result<ConfirmDeliveryScanResponse>>;

public record ConfirmDeliveryScanResponse(string Status, string Message);
