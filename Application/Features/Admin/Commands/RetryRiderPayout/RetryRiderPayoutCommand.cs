using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Admin.Commands.RetryRiderPayout;

public sealed record RetryRiderPayoutCommand(Guid OrderId) : IRequest<Result<OrderDto>>;
