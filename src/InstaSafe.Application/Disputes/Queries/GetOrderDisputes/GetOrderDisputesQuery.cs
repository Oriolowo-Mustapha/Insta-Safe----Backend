using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Disputes.Queries.GetOrderDisputes;

public record GetOrderDisputesQuery(Guid OrderId) : IRequest<Result<DisputeDto?>>;
