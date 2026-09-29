using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.MarkFundsHeld;

public sealed record MarkFundsHeldCommand(
    string PaystackReference,
    string? CustomerEmail = null,
    long? AmountKobo = null
) : IRequest<Result<OrderDto>>;
