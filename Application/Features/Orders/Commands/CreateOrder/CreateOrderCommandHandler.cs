using AutoMapper;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Notifications;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using InstaSafe.Domain.Exceptions;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.CreateOrder;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IVendorRepository _vendors;
    private readonly IDispatcherRepository _drivers;
    private readonly IPaystackClient _paystack;
    private readonly ISanitizer _sanitizer;
    private readonly OrderNotifier _notifier;
    private readonly IMapper _mapper;

    public CreateOrderCommandHandler(
        IOrderRepository orders, IVendorRepository vendors, IDispatcherRepository drivers,
        IPaystackClient paystack, ISanitizer sanitizer, OrderNotifier notifier, IMapper mapper)
    {
        _orders = orders;
        _vendors = vendors;
        _drivers = drivers;
        _paystack = paystack;
        _sanitizer = sanitizer;
        _notifier = notifier;
        _mapper = mapper;
    }

    public async Task<Result<OrderDto>> Handle(CreateOrderCommand req, CancellationToken ct)
    {
        if (req.AmountNgn <= 0)
            throw new DomainValidationException("Amount must be greater than zero.");
        if (req.Items.Count == 0)
            throw new DomainValidationException("At least one item is required.");

        var buyerEmail = _sanitizer.Clean(req.BuyerEmail?.Trim(), 200);
        var order = new Order
        {
            VendorPhone = _sanitizer.Clean(req.VendorPhone, 20),
            CustomerName = _sanitizer.Clean(req.CustomerName, 120),
            CustomerPhone = _sanitizer.Clean(req.CustomerPhone, 20),
            BuyerEmail = string.IsNullOrWhiteSpace(buyerEmail) ? null : buyerEmail,
            DeliveryAddress = _sanitizer.Clean(req.DeliveryAddress, 500),
            Items = req.Items.Select(i => new OrderItem
            {
                Description = _sanitizer.Clean(i.Description, 300),
                Quantity = i.Quantity <= 0 ? 1 : i.Quantity,
                UnitPriceKobo = i.UnitPriceNgn * 100
            }).ToList(),
            AmountKobo = (req.AmountNgn + req.DeliveryFeeNgn) * 100,
            Currency = "NGN",
            Status = OrderStatus.AwaitingPayment,
            Fulfillment = req.Fulfillment,
            DeliveryFeeKobo = req.Fulfillment == FulfillmentType.Digital ? 0 : req.DeliveryFeeNgn * 100,
            OrderNumber = await NextOrderNumberAsync(ct)
        };

        if (string.IsNullOrWhiteSpace(order.VendorPhone))
            throw new DomainValidationException("Vendor phone is required.");
        if (string.IsNullOrWhiteSpace(order.CustomerPhone))
            throw new DomainValidationException("Customer phone is required.");

        var vendor = await _vendors.GetByPhoneAsync(order.VendorPhone, ct);
        if (vendor is null)
        {
            vendor = new Vendor { Phone = order.VendorPhone, DisplayName = order.VendorPhone, IsActive = true };
            await _vendors.AddAsync(vendor, ct);
        }
        order.VendorId = vendor.Id;

        var payoutAccount = req.VendorAccountNumber ?? vendor.AccountNumber;
        var payoutBank = req.VendorBankCode ?? vendor.BankCode;
        if (payoutAccount is not null && payoutBank is not null)
        {
            var recipient = await _paystack.CreateRecipientAsync(payoutAccount, payoutBank, order.VendorPhone, ct);
            if (recipient is not null)
            {
                order.VendorRecipientCode = recipient;
                if (req.VendorAccountNumber is not null && req.VendorBankCode is not null)
                {
                    vendor.AccountNumber = req.VendorAccountNumber;
                    vendor.BankCode = req.VendorBankCode;
                    vendor.PaystackRecipientCode = recipient;
                    vendor.Touch();
                }
            }
        }
        else if (vendor.PaystackRecipientCode is not null)
        {
            order.VendorRecipientCode = vendor.PaystackRecipientCode;
        }

        if (order.Fulfillment == FulfillmentType.Dispatch && !string.IsNullOrWhiteSpace(req.DriverPhone))
        {
            // Driver payout details live per-order only (drivers hold no accounts).
            var driverPhone = PhoneNormalizer.Normalize(req.DriverPhone);
            var driver = await _drivers.GetByPhoneAsync(driverPhone, ct);
            if (driver is not null)
                order.DriverId = driver.Id;
            order.DriverPhone = driverPhone;

            if (req.DriverAccountNumber is not null && req.DriverBankCode is not null)
            {
                var recipient = await _paystack.CreateRecipientAsync(
                    req.DriverAccountNumber, req.DriverBankCode, driverPhone, ct);
                if (recipient is not null)
                    order.DriverRecipientCode = recipient;
            }
        }

        await _orders.AddAsync(order, ct);

        var (reference, authUrl) = await _paystack.InitializeTransactionAsync(req.BuyerEmail!, order.AmountKobo, order.Id, ct);        order.PaystackReference = reference;
        order.PaystackAuthUrl = authUrl;
        order.Status = OrderStatus.AwaitingPayment;
        order.Touch();

        order.AddDomainEvent(new OrderCreatedEvent(order.Id));
        await _orders.SaveAsync(ct);

        await _notifier.OrderCreatedAsync(order, OrderNotifier.IsRealEmail(order.BuyerEmail) ? order.BuyerEmail : null);
        await _notifier.PaymentLinkAsync(order);
        if (order.DriverPhone is not null)
            await _notifier.DriverAssignedAsync(order);

        return Result<OrderDto>.Success(_mapper.Map<OrderDto>(order));
    }

    private async Task<string> NextOrderNumberAsync(CancellationToken ct)
    {
        for (var i = 0; i < 3; i++)
        {
            var candidate = OrderNumberGenerator.Generate();
            if (await _orders.GetByOrderNumberAsync(candidate, ct) is null)
                return candidate;
        }
        return OrderNumberGenerator.Generate();
    }
}
