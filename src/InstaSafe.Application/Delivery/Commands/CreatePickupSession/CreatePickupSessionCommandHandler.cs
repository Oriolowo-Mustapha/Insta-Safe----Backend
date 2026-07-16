using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using MediatR;

namespace InstaSafe.Application.Delivery.Commands.CreatePickupSession;

public class CreatePickupSessionCommandHandler : IRequestHandler<CreatePickupSessionCommand, Result<CreatePickupSessionResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IQrTokenService _qrTokenService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreatePickupSessionCommandHandler(
        IUnitOfWork unitOfWork,
        IQrTokenService qrTokenService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _qrTokenService = qrTokenService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<CreatePickupSessionResponse>> Handle(CreatePickupSessionCommand request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetByIdWithMerchantAsync(request.OrderId, cancellationToken);

        if (order == null)
            return Result<CreatePickupSessionResponse>.Failure("Order not found.");

        if (order.Status != OrderStatus.FundedInEscrow)
            return Result<CreatePickupSessionResponse>.Failure("Order must be funded before pickup.");

        var payload = _qrTokenService.ValidateAndDecodeToken(request.MerchantQrToken);
        if (payload == null)
            return Result<CreatePickupSessionResponse>.Failure("Invalid or expired QR token.");

        if (payload.OrderId != request.OrderId)
            return Result<CreatePickupSessionResponse>.Failure("QR token does not match this order.");

        if (payload.ActorId != order.MerchantId)
            return Result<CreatePickupSessionResponse>.Failure("QR token was not issued for this merchant.");

        var sessionId = Guid.NewGuid();
        var expiresAt = _dateTimeProvider.UtcNow.AddHours(4);

        var session = new DeliverySession
        {
            OrderId = order.Id,
            SessionId = sessionId,
            PickupDeviceFingerprint = request.DeviceFingerprint,
            PickupTimestamp = _dateTimeProvider.UtcNow,
            PickupLatitude = request.Latitude,
            PickupLongitude = request.Longitude,
            ExpiresAt = expiresAt
        };

        _unitOfWork.DeliverySessions.Add(session);

        order.Dispatch();
        order.AddDomainEvent(new OrderDispatchedEvent(order.Id, sessionId));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CreatePickupSessionResponse>.Success(new CreatePickupSessionResponse(sessionId, "PickedUp", expiresAt));
    }
}
