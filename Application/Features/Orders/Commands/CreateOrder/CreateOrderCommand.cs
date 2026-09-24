using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Domain.Enums;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.CreateOrder;

public sealed record OrderItemInput(string Description, int Quantity, long UnitPriceNgn);

public sealed record CreateOrderCommand(
    string VendorPhone, string CustomerName, string CustomerPhone,
    string DeliveryAddress, List<OrderItemInput> Items, long AmountNgn,
    string BuyerEmail, string? VendorAccountNumber = null, string? VendorBankCode = null,
    FulfillmentType Fulfillment = FulfillmentType.Dispatch, long DeliveryFeeNgn = 0,
    string? DriverPhone = null, string? DriverAccountNumber = null, string? DriverBankCode = null
) : IRequest<Result<OrderDto>>;
