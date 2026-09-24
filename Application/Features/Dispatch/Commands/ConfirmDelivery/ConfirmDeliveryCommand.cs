using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Dispatch.Commands.ConfirmDelivery;

public sealed record ConfirmDeliveryCommand(Guid DispatcherId, string DriverPhone, Guid OrderId, string Otp)
    : IRequest<Result<OrderDto>>;
