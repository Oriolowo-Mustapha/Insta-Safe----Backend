using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Delivery.Commands.GenerateDeliveryQrCodes;

public record GenerateDeliveryQrCodesCommand(Guid OrderId) : IRequest<Result<DeliveryQrCodesResponse>>;

public record DeliveryQrCodesResponse(string MerchantQrToken, string BuyerQrToken);
