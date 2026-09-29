using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Features.Orders.DTOs;
using MediatR;

namespace InstaSafe.Application.Features.Orders.Queries.GetOrderByReference;

public sealed record GetOrderByReferenceQuery(string Reference) : IRequest<Result<PublicOrderDto>>;
