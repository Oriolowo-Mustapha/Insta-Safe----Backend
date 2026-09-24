using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Features.Dispatch.Commands.RequestDispatcherOtp;

public sealed record RequestDispatcherOtpCommand(string Phone) : IRequest<Result<bool>>;
