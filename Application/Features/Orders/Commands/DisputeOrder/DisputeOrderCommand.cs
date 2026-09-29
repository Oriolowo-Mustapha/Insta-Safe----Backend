using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Commands.DisputeOrder;

public sealed record DisputeOrderCommand(Guid OrderId, string Reason) : IRequest<Result<PublicOrderDto>>;
