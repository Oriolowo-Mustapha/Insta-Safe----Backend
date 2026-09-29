using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Notifications;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Exceptions;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.RequestBankTransfer;

public class RequestBankTransferCommandHandler : IRequestHandler<RequestBankTransferCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IPaystackClient _paystack;
    private readonly OrderNotifier _notifier;
    private readonly IMapper _mapper;

    public RequestBankTransferCommandHandler(
        IOrderRepository orders, IPaystackClient paystack, OrderNotifier notifier, IMapper mapper)
    {
        _orders = orders; _paystack = paystack; _notifier = notifier; _mapper = mapper;
    }

    public async Task<Result<OrderDto>> Handle(RequestBankTransferCommand req, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(req.OrderId, ct);
        if (order is null)
            return Result<OrderDto>.Failure("Order not found.");
        if (order.Status is not (OrderStatus.AwaitingPayment or OrderStatus.Draft))
            throw new ConflictException($"Cannot issue a transfer account from status {order.Status}.");

        if (!string.IsNullOrWhiteSpace(order.PayVirtualAccountNumber))
            return Result<OrderDto>.Success(_mapper.Map<OrderDto>(order));

        var buyerEmail = string.IsNullOrWhiteSpace(order.BuyerEmail)
            ? $"{order.CustomerPhone}@whatsapp.instasafe"
            : order.BuyerEmail;

        if (string.IsNullOrWhiteSpace(order.PaystackCustomerCode))
        {
            var (ok, code, error) = await _paystack.CreateCustomerAsync(
                buyerEmail, order.CustomerName, "Customer", order.CustomerPhone, order.Id, ct);
            if (!ok)
                return Result<OrderDto>.Failure(error ?? "Could not set up bank transfer.");
            order.PaystackCustomerCode = code;
            order.Touch();
            await _orders.SaveAsync(ct);
        }

        var (assigned, number, name, bank, assignError) = await _paystack.AssignDedicatedAccountAsync(
            order.PaystackCustomerCode!, req.PreferredBank, ct);
        if (!assigned)
            return Result<OrderDto>.Failure(assignError ?? "Could not issue a transfer account.");

        order.PayVirtualAccountNumber = number;
        order.PayVirtualAccountName = name;
        order.PayVirtualAccountBank = bank;
        order.Touch();
        await _orders.SaveAsync(ct);

        await _notifier.BankTransferDetailsAsync(order);

        return Result<OrderDto>.Success(_mapper.Map<OrderDto>(order));
    }
}
