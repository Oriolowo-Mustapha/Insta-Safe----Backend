using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.MarkFundsHeld;

public sealed record MarkFundsHeldCommand(string PaystackReference) : IRequest<Result<OrderDto>>;
