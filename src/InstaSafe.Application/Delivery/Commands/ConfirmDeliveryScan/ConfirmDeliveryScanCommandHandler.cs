using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using MediatR;

namespace InstaSafe.Application.Delivery.Commands.ConfirmDeliveryScan;

public class ConfirmDeliveryScanCommandHandler : IRequestHandler<ConfirmDeliveryScanCommand, Result<ConfirmDeliveryScanResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IQrTokenService _qrTokenService;
    private readonly IFingerprintMatcher _fingerprintMatcher;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ConfirmDeliveryScanCommandHandler(
        IUnitOfWork unitOfWork,
        IQrTokenService qrTokenService,
        IFingerprintMatcher fingerprintMatcher,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _qrTokenService = qrTokenService;
        _fingerprintMatcher = fingerprintMatcher;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<ConfirmDeliveryScanResponse>> Handle(ConfirmDeliveryScanCommand request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetByIdWithBuyerAndSessionAsync(request.OrderId, cancellationToken);

        if (order == null)
            return Result<ConfirmDeliveryScanResponse>.Failure("Order not found.");

        if (order.Status != OrderStatus.Dispatched)
            return Result<ConfirmDeliveryScanResponse>.Failure("Order must be dispatched before delivery confirmation.");

        if (order.DeliverySession == null || order.DeliverySession.SessionId != request.SessionId)
            return Result<ConfirmDeliveryScanResponse>.Failure("Delivery session not found for this order.");

        var session = order.DeliverySession;

        if (session.Status != DeliverySessionStatus.PickedUp)
            return Result<ConfirmDeliveryScanResponse>.Failure("Session is not in a valid state for delivery confirmation.");

        if (session.ExpiresAt.HasValue && session.ExpiresAt.Value < _dateTimeProvider.UtcNow)
        {
            session.Expire();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ConfirmDeliveryScanResponse>.Failure("Delivery session has expired.");
        }

        var payload = _qrTokenService.ValidateAndDecodeToken(request.BuyerQrToken);
        if (payload == null)
        {
            session.MarkAsFailed(DeliveryFailureReason.SessionMismatch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ConfirmDeliveryScanResponse>.Failure("Invalid or expired QR token.");
        }

        if (payload.OrderId != request.OrderId)
        {
            session.MarkAsFailed(DeliveryFailureReason.OrderMismatch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ConfirmDeliveryScanResponse>.Failure("QR token does not match this order.");
        }

        if (order.BuyerId == null || payload.ActorId != order.BuyerId.Value)
        {
            session.MarkAsFailed(DeliveryFailureReason.SessionMismatch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ConfirmDeliveryScanResponse>.Failure("QR token was not issued for this buyer.");
        }

        if (!string.IsNullOrEmpty(session.PickupDeviceFingerprint))
        {
            var fingerprintMatch = _fingerprintMatcher.IsMatch(
                session.PickupDeviceFingerprint, request.DeviceFingerprint);

            if (!fingerprintMatch)
            {
                session.MarkAsFailed(DeliveryFailureReason.FingerprintMismatch);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result<ConfirmDeliveryScanResponse>.Failure("Device fingerprint does not match pickup fingerprint.");
            }
        }

        var now = _dateTimeProvider.UtcNow;
        session.MarkAsDelivered(request.DeviceFingerprint, request.Latitude, request.Longitude, now);
        order.Deliver(now, now.AddHours(24), session.SessionId);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ConfirmDeliveryScanResponse>.Success(new ConfirmDeliveryScanResponse(
            "Delivered",
            "Delivery confirmed. A 24-hour validation window has started for the buyer."));
    }
}
