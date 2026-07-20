using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Orders.Commands.CreateOrder;

public record CreateOrderCommand(
    Guid MerchantId,
    string ItemName,
    string? ItemDescription,
    string? ItemImageUrl,
    decimal Price,
    string? DeliveryAddress,
    string BuyerFirstName,
    string BuyerLastName,
    string BuyerEmail,
    string BuyerPhone,
    string? DispatcherPhone
) : IRequest<Result<CreateOrderResponse>>;

public record CreateOrderResponse(Guid OrderId, string OrderReference, string CheckoutUrl);
