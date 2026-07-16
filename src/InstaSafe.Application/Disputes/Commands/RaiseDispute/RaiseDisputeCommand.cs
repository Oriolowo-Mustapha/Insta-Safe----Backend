using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Disputes.Commands.RaiseDispute;

public record RaiseDisputeCommand(
    Guid OrderId,
    Guid BuyerId,
    string Reason,
    string? EvidenceUrls
) : IRequest<Result<RaiseDisputeResponse>>;

public record RaiseDisputeResponse(Guid DisputeId, string Status, string Message);
