using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Queries.ListOrders;

public sealed record ListOrdersQuery(int Page = 1, int PageSize = 20) : IRequest<Result<List<OrderDto>>>;
