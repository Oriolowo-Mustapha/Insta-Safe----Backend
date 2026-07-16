using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Disputes.Queries.GetDispute;

public record GetDisputeQuery(Guid DisputeId) : IRequest<Result<DisputeDto>>;
