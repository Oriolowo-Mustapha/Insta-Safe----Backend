using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Disputes.Commands.ResolveDispute;

public record ResolveDisputeCommand(
    Guid DisputeId,
    string Resolution,       // "refund" or "release"
    string? AdminNotes,
    string ResolvedByUserId
) : IRequest<Result<ResolveDisputeResponse>>;

public record ResolveDisputeResponse(string Outcome, string Message);
