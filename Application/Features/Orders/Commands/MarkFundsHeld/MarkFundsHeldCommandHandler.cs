using AutoMapper;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using InstaSafe.Domain.Exceptions;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.MarkFundsHeld;

public class MarkFundsHeldCommandHandler : IRequestHandler<MarkFundsHeldCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IAppDbContext _db;
    private readonly IPaystackClient _paystack;
    private readonly IOtpService _otp;
    private readonly IWhatsAppSender _wa;
    private readonly IMapper _mapper;

    public MarkFundsHeldCommandHandler(
        IOrderRepository orders, IAppDbContext db,
        IPaystackClient paystack, IOtpService otp, IWhatsAppSender wa, IMapper mapper)
    {
        _orders = orders; _db = db; _paystack = paystack; _otp = otp; _wa = wa; _mapper = mapper;
    }

    public async Task<Result<OrderDto>> Handle(MarkFundsHeldCommand req, CancellationToken ct)
    {
        var order = await _orders.GetByPaystackRefAsync(req.PaystackReference, ct);
        if (order is null)
            return Result<OrderDto>.Failure($"Order with reference '{req.PaystackReference}' not found.");

        if (order.Status != OrderStatus.AwaitingPayment && order.Status != OrderStatus.Draft)
            throw new ConflictException($"Cannot hold funds from status {order.Status}.");

        if (!await _paystack.VerifyTransactionAsync(req.PaystackReference, ct))
            return Result<OrderDto>.Failure("Paystack verification failed.");

        var code = _otp.GenerateOtp();
        var salt = _otp.NewSalt();

        order.Status = OrderStatus.Held;
        order.HeldAt = DateTimeOffset.UtcNow;
        order.OtpHash = _otp.Hash(code, salt) + "." + salt;
        order.OtpExpiresAt = DateTimeOffset.UtcNow.AddHours(24);
        order.OtpAttempts = 0;
        order.Touch();
        order.AddDomainEvent(new FundsHeldEvent(order.Id, order.AmountKobo));

        _db.Ledgers.Add(new EscrowLedger
        {
            OrderId = order.Id,
            AmountKobo = order.AmountKobo,
            Currency = order.Currency
        });
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Type = "FundsHeld",
            Payload = System.Text.Json.JsonSerializer.Serialize(new { order.Id, order.AmountKobo })
        });
        await _orders.SaveAsync(ct);

        try
        {
            await _wa.SendTextAsync(order.CustomerPhone,
                $"InstaSafe: your delivery code is {code}. Share it with the rider only when you receive your item.", ct);
        }
        catch { /* outbox covers retry; MVP keeps webhook fast */ }

        return Result<OrderDto>.Success(_mapper.Map<OrderDto>(order));
    }
}
