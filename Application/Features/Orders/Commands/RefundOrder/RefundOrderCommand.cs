using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.RefundOrder;

public sealed record RefundOrderCommand(Guid OrderId) : IRequest<Result<OrderDto>>;
