using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.VerifyOtp;

public sealed record VerifyOtpCommand(Guid OrderId, string Otp) : IRequest<Result<OrderDto>>;
