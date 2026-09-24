using AutoMapper;
using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Dispatch.DTOs;
using InstaSafe.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace InstaSafe.Application.Features.Dispatch.Commands.VerifyDispatcherOtp;

public class VerifyDispatcherOtpCommandHandler : IRequestHandler<VerifyDispatcherOtpCommand, Result<DispatcherAuthResponse>>
{
    private readonly IDispatcherRepository _dispatchers;
    private readonly IOtpService _otp;
    private readonly IJwtTokenService _tokens;
    private readonly IMapper _mapper;
    private readonly IConfiguration _config;

    public VerifyDispatcherOtpCommandHandler(
        IDispatcherRepository dispatchers, IOtpService otp, IJwtTokenService tokens,
        IMapper mapper, IConfiguration config)
    {
        _dispatchers = dispatchers; _otp = otp; _tokens = tokens; _mapper = mapper; _config = config;
    }

    public async Task<Result<DispatcherAuthResponse>> Handle(VerifyDispatcherOtpCommand req, CancellationToken ct)
    {
        var phone = PhoneNormalizer.Normalize(req.Phone);
        var dispatcher = await _dispatchers.GetByPhoneAsync(phone, ct);
        if (dispatcher is null || !dispatcher.IsActive)
            return Result<DispatcherAuthResponse>.Failure("No active dispatcher found for this phone.");
        if (dispatcher.OtpHash is null || !dispatcher.OtpHash.Contains('.'))
            return Result<DispatcherAuthResponse>.Failure("No login code requested. Request one first.");
        if (dispatcher.OtpExpiresAt is null || DateTimeOffset.UtcNow > dispatcher.OtpExpiresAt)
            throw new DomainValidationException("Login code has expired. Request a new one.");
        if (dispatcher.OtpAttempts >= 5)
            throw new ForbiddenAccessException("Too many attempts. Request a new code.");

        var salt = dispatcher.OtpHash.Split('.', 2)[1];
        if (dispatcher.OtpHash != _otp.Hash(req.Code.Trim(), salt) + "." + salt)
        {
            dispatcher.OtpAttempts++;
            dispatcher.Touch();
            await _dispatchers.SaveAsync(ct);
            throw new DomainValidationException("Invalid login code.");
        }

        dispatcher.OtpHash = null;
        dispatcher.OtpExpiresAt = null;
        dispatcher.OtpAttempts = 0;
        dispatcher.Touch();
        await _dispatchers.SaveAsync(ct);

        var hours = double.TryParse(_config["Auth:TokenHours"], out var h) && h > 0 ? h : 24;
        var token = _tokens.CreateDispatcherToken(dispatcher.Id, dispatcher.Phone);
        return Result<DispatcherAuthResponse>.Success(
            new DispatcherAuthResponse(token, _mapper.Map<DispatcherDto>(dispatcher), hours));
    }
}
