using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.ConfirmSatisfaction;

public sealed record ConfirmSatisfactionCommand(Guid OrderId) : IRequest<Result<OrderDto>>;
