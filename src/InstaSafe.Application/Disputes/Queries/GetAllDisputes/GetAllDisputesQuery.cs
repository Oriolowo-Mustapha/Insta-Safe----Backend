using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Disputes.Queries.GetAllDisputes;

public record GetAllDisputesQuery() : IRequest<Result<List<DisputeDto>>>;
