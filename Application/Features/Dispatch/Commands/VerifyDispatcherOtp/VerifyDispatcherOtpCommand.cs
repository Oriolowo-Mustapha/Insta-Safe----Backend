using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Dispatch.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Dispatch.Commands.VerifyDispatcherOtp;

public sealed record VerifyDispatcherOtpCommand(string Phone, string Code)
    : IRequest<Result<DispatcherAuthResponse>>;

public sealed record DispatcherAuthResponse(string Token, DispatcherDto Dispatcher, double ExpiresInHours);
