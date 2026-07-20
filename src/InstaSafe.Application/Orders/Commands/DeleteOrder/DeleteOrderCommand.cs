using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Orders.Commands.DeleteOrder;

public record DeleteOrderCommand(Guid OrderId, Guid MerchantId) : IRequest<Result<bool>>;
