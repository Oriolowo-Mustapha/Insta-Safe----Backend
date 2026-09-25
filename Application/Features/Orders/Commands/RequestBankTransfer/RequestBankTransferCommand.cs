using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.RequestBankTransfer;

public sealed record RequestBankTransferCommand(Guid OrderId, string? PreferredBank = null)
    : IRequest<Result<OrderDto>>;
