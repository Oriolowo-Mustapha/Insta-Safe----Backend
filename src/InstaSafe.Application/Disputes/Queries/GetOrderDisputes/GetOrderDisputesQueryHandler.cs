using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Disputes.Queries.GetOrderDisputes;

public class GetOrderDisputesQueryHandler : IRequestHandler<GetOrderDisputesQuery, Result<DisputeDto?>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetOrderDisputesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DisputeDto?>> Handle(GetOrderDisputesQuery request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetByIdWithAllAsync(request.OrderId, cancellationToken);
        if (order == null)
            return Result<DisputeDto?>.Failure("Order not found.");

        if (order.Dispute == null)
            return Result<DisputeDto?>.Success(null);

        var dto = new DisputeDto(
            order.Dispute.Id,
            order.Dispute.OrderId,
            order.OrderReference,
            order.Dispute.RaisedByBuyerId,
            order.Buyer != null ? $"{order.Buyer.FirstName} {order.Buyer.LastName}" : "Unknown",
            order.Dispute.Reason,
            order.Dispute.EvidenceUrls,
            order.Dispute.Status.ToString(),
            order.Dispute.Resolution,
            order.Dispute.ResolvedAt,
            order.Dispute.ResolvedBy,
            order.Dispute.CreatedAt,
            order.Dispute.AiConfidenceScore,
            order.Dispute.AiAnalysisSummary
        );

        return Result<DisputeDto?>.Success(dto);
    }
}
