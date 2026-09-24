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
            DeliveryFeeKobo = req.Fulfillment == FulfillmentType.Digital ? 0 : req.DeliveryFeeNgn * 100
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
            var driverPhone = PhoneNormalizer.Normalize(req.DriverPhone);
            var driver = await _drivers.GetByPhoneAsync(driverPhone, ct);
            if (driver is null)
            {
                driver = new Dispatcher { Phone = driverPhone, IsActive = true };
                await _drivers.AddAsync(driver, ct);
            }
            order.DriverId = driver.Id;
            order.DriverPhone = driver.Phone;

            var driverAccount = req.DriverAccountNumber ?? driver.AccountNumber;
            var driverBank = req.DriverBankCode ?? driver.BankCode;
            if (driverAccount is not null && driverBank is not null)
            {
                var recipient = await _paystack.CreateRecipientAsync(driverAccount, driverBank, driver.Phone, ct);
                if (recipient is not null)
                {
                    order.DriverRecipientCode = recipient;
                    if (req.DriverAccountNumber is not null && req.DriverBankCode is not null)
                    {
                        driver.AccountNumber = req.DriverAccountNumber;
                        driver.BankCode = req.DriverBankCode;
                        driver.PaystackRecipientCode = recipient;
                        driver.Touch();
                    }
                }
            }
            else if (driver.PaystackRecipientCode is not null)
            {
                order.DriverRecipientCode = driver.PaystackRecipientCode;
            }
        }

        await _orders.AddAsync(order, ct);

        var (reference, authUrl) = await _paystack.InitializeTransactionAsync(req.BuyerEmail!, order.AmountKobo, order.Id, ct);
        order.PaystackReference = reference;
        order.PaystackAuthUrl = authUrl;
        order.Status = OrderStatus.AwaitingPayment;
        order.Touch();

        order.AddDomainEvent(new OrderCreatedEvent(order.Id));
        await _orders.SaveAsync(ct);

        await _notifier.OrderCreatedAsync(order, OrderNotifier.IsRealEmail(order.BuyerEmail) ? order.BuyerEmail : null);
        if (order.DriverPhone is not null)
            await _notifier.DriverAssignedAsync(order);

        return Result<OrderDto>.Success(_mapper.Map<OrderDto>(order));
    }
}
