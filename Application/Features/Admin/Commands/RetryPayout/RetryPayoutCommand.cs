using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Admin.Commands.RetryPayout;

public sealed record RetryPayoutCommand(Guid OrderId) : IRequest<Result<OrderDto>>;
