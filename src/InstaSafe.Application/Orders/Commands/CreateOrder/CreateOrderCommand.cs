using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Orders.Commands.CreateOrder;

public record CreateOrderCommand(
    Guid MerchantId,
    string ItemName,
    string? ItemDescription,
    string? ItemImageUrl,
    decimal Price,
    string? DeliveryAddress
) : IRequest<Result<CreateOrderResponse>>;

public record CreateOrderResponse(Guid OrderId, string OrderReference);
