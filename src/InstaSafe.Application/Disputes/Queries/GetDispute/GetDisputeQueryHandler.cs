using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Entities;
using MediatR;

namespace InstaSafe.Application.Disputes.Queries.GetDispute;

public class GetDisputeQueryHandler : IRequestHandler<GetDisputeQuery, Result<DisputeDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetDisputeQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<DisputeDto>> Handle(GetDisputeQuery request, CancellationToken cancellationToken)
    {
        var dispute = await _unitOfWork.Repository<Dispute>().GetByIdAsync(request.DisputeId, cancellationToken);
        if (dispute == null)
            return Result<DisputeDto>.Failure("Dispute not found.");

        var order = await _unitOfWork.Orders.GetByIdWithAllAsync(dispute.OrderId, cancellationToken);
        if (order == null)
            return Result<DisputeDto>.Failure("Order data is missing.");

        var dto = new DisputeDto(
            dispute.Id,
            dispute.OrderId,
            order.OrderReference,
            dispute.RaisedByBuyerId,
            order.Buyer != null ? $"{order.Buyer.FirstName} {order.Buyer.LastName}" : "Unknown",
            dispute.Reason,
            dispute.EvidenceUrls,
            dispute.Status.ToString(),
            dispute.Resolution,
            dispute.ResolvedAt,
            dispute.ResolvedBy,
            dispute.CreatedAt
        );

        return Result<DisputeDto>.Success(dto);
    }
}
