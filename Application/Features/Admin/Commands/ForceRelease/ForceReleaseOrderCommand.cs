using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Admin.Commands.ForceRelease;

public sealed record ForceReleaseOrderCommand(Guid OrderId, string? Note = null)
    : IRequest<Result<OrderDto>>;
