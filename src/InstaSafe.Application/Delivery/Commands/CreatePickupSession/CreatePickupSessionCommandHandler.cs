using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Delivery.Commands.CreatePickupSession;

public class CreatePickupSessionCommandHandler : IRequestHandler<CreatePickupSessionCommand, Result<CreatePickupSessionResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;
    private readonly IQrTokenService _qrTokenService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMediator _mediator;
    private readonly IWhatsAppMessagingService _whatsAppMessagingService;
    private readonly ILogger<CreatePickupSessionCommandHandler> _logger;

    public CreatePickupSessionCommandHandler(
        IUnitOfWork unitOfWork,
        IApplicationDbContext context,
        IQrTokenService qrTokenService,
        IDateTimeProvider dateTimeProvider,
        IMediator mediator,
        IWhatsAppMessagingService whatsAppMessagingService,
        ILogger<CreatePickupSessionCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _context = context;
        _qrTokenService = qrTokenService;
        _dateTimeProvider = dateTimeProvider;
        _mediator = mediator;
        _whatsAppMessagingService = whatsAppMessagingService;
        _logger = logger;
    }

    public async Task<Result<CreatePickupSessionResponse>> Handle(CreatePickupSessionCommand request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders
            .GetByIdWithMerchantAsync(request.OrderId, cancellationToken);
            
        var buyer = order?.BuyerId != null ? await _context.Buyers.FirstOrDefaultAsync(b => b.Id == order.BuyerId.Value, cancellationToken) : null;

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

        if (buyer != null && !string.IsNullOrEmpty(buyer.Phone))
        {
            var qrResult = await _mediator.Send(new InstaSafe.Application.Delivery.Commands.GenerateDeliveryQrCodes.GenerateDeliveryQrCodesCommand(order.Id), cancellationToken);
            if (qrResult.Succeeded && qrResult.Data != null)
            {
                var msg = $"InstaSafe Alert 🚚\n\nYour order #{order.OrderReference} ({order.ItemName}) has been picked up and is on its way!\n\n" +
                          $"When the dispatcher arrives, please show them this Delivery QR Code Token to confirm receipt:\n" +
                          $"*{qrResult.Data.BuyerQrToken}*";
                await _whatsAppMessagingService.SendMessageAsync(buyer.Phone, msg, cancellationToken);
                _logger.LogInformation("Sent Delivery QR to buyer {BuyerPhone}", buyer.Phone);
            }
        }

        return Result<CreatePickupSessionResponse>.Success(new CreatePickupSessionResponse(sessionId, "PickedUp", expiresAt));
    }
}
