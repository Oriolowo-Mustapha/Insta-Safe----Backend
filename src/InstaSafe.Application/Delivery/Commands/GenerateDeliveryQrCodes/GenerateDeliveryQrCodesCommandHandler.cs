using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Enums;
using MediatR;

namespace InstaSafe.Application.Delivery.Commands.GenerateDeliveryQrCodes;

public class GenerateDeliveryQrCodesCommandHandler : IRequestHandler<GenerateDeliveryQrCodesCommand, Result<DeliveryQrCodesResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IQrTokenService _qrTokenService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GenerateDeliveryQrCodesCommandHandler(
        IUnitOfWork unitOfWork,
        IQrTokenService qrTokenService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _qrTokenService = qrTokenService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<DeliveryQrCodesResponse>> Handle(GenerateDeliveryQrCodesCommand request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetByIdWithMerchantAsync(request.OrderId, cancellationToken);

        if (order == null)
            return Result<DeliveryQrCodesResponse>.Failure("Order not found.");

        if (order.Status != OrderStatus.FundedInEscrow)
            return Result<DeliveryQrCodesResponse>.Failure("QR codes can only be generated for funded orders.");

        if (order.MerchantId == Guid.Empty)
            return Result<DeliveryQrCodesResponse>.Failure("Order has no merchant assigned.");

        if (order.BuyerId == null)
            return Result<DeliveryQrCodesResponse>.Failure("Order has no buyer assigned.");

        var now = _dateTimeProvider.UtcNow;
        var expiresAt = now.AddMinutes(15);

        var merchantPayload = new QrPayload(
            order.Id, order.MerchantId, Guid.NewGuid().ToString("N"), now, expiresAt);

        var buyerPayload = new QrPayload(
            order.Id, order.BuyerId.Value, Guid.NewGuid().ToString("N"), now, expiresAt);

        var merchantToken = _qrTokenService.GenerateSignedToken(merchantPayload);
        var buyerToken = _qrTokenService.GenerateSignedToken(buyerPayload);

        return Result<DeliveryQrCodesResponse>.Success(new DeliveryQrCodesResponse(merchantToken, buyerToken));
    }
}
