using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.ResolveDispute;

public enum DisputeResolution
{
    Release = 0,
    Refund = 1
}

public sealed record ResolveDisputeCommand(Guid OrderId, DisputeResolution Resolution)
    : IRequest<Result<OrderDto>>;
