using AutoMapper;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Dispatch.DTOs;
using InstaSafe.Domain.Entities;
using MediatR;

namespace InstaSafe.Application.Features.Dispatch.Commands.RegisterDispatcher;

public class RegisterDispatcherCommandHandler : IRequestHandler<RegisterDispatcherCommand, Result<DispatcherDto>>
{
    private readonly IDispatcherRepository _dispatchers;
    private readonly IPaystackClient _paystack;
    private readonly ISanitizer _sanitizer;
    private readonly IMapper _mapper;

    public RegisterDispatcherCommandHandler(
        IDispatcherRepository dispatchers, IPaystackClient paystack, ISanitizer sanitizer, IMapper mapper)
    {
        _dispatchers = dispatchers; _paystack = paystack; _sanitizer = sanitizer; _mapper = mapper;
    }

    public async Task<Result<DispatcherDto>> Handle(RegisterDispatcherCommand req, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(req.Phone);
        if (string.IsNullOrWhiteSpace(phone))
            return Result<DispatcherDto>.Failure("Dispatcher phone is required.");

        if (await _dispatchers.ExistsByPhoneAsync(phone, ct))
            return Result<DispatcherDto>.Failure($"Dispatcher with phone '{phone}' already exists.");

        var dispatcher = new Dispatcher
        {
            Phone = _sanitizer.Clean(phone, 20),
            FirstName = req.FirstName is null ? null : _sanitizer.Clean(req.FirstName, 120),
            LastName = req.LastName is null ? null : _sanitizer.Clean(req.LastName, 120),
            AccountNumber = req.AccountNumber is null ? null : _sanitizer.Clean(req.AccountNumber, 20),
            BankCode = req.BankCode is null ? null : _sanitizer.Clean(req.BankCode, 10),
            IsActive = true
        };

        if (dispatcher.AccountNumber is not null && dispatcher.BankCode is not null)
        {
            var recipient = await _paystack.CreateRecipientAsync(
                dispatcher.AccountNumber, dispatcher.BankCode, dispatcher.Phone, ct);
            if (recipient is not null) dispatcher.PaystackRecipientCode = recipient;
        }

        await _dispatchers.AddAsync(dispatcher, ct);
        await _dispatchers.SaveAsync(ct);

        return Result<DispatcherDto>.Success(_mapper.Map<DispatcherDto>(dispatcher));
    }
}
