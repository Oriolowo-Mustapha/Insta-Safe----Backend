using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Queries.GetOrderTimeline;

public sealed record GetOrderTimelineQuery(string Reference) : IRequest<Result<OrderTimelineDto>>;
