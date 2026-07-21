using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Entities;
using MediatR;

namespace InstaSafe.Application.Disputes.Queries.GetAllDisputes;

public class GetAllDisputesQueryHandler : IRequestHandler<GetAllDisputesQuery, Result<List<DisputeDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAllDisputesQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<List<DisputeDto>>> Handle(GetAllDisputesQuery request, CancellationToken cancellationToken)
    {
        var disputes = await _unitOfWork.Repository<Dispute>().GetAllAsync(cancellationToken);
        var dtos = new List<DisputeDto>();

        foreach (var dispute in disputes)
        {
            var order = await _unitOfWork.Orders.GetByIdWithAllAsync(dispute.OrderId, cancellationToken);
            dtos.Add(new DisputeDto(
                dispute.Id,
                dispute.OrderId,
                order?.OrderReference ?? "Unknown",
                dispute.RaisedByBuyerId,
                order?.Buyer != null ? $"{order.Buyer.FirstName} {order.Buyer.LastName}" : "Unknown",
                dispute.Reason,
                dispute.EvidenceUrls,
                dispute.Status.ToString(),
                dispute.Resolution,
                dispute.ResolvedAt,
                dispute.ResolvedBy,
                dispute.CreatedAt,
                dispute.AiConfidenceScore,
                dispute.AiAnalysisSummary
            ));
        }

        return Result<List<DisputeDto>>.Success(dtos);
    }
}
