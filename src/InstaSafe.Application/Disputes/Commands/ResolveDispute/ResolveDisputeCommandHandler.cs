using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using MediatR;

namespace InstaSafe.Application.Disputes.Commands.ResolveDispute;

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand, Result<ResolveDisputeResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ResolveDisputeCommandHandler(IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<ResolveDisputeResponse>> Handle(ResolveDisputeCommand request, CancellationToken cancellationToken)
    {
        var dispute = await _unitOfWork.Repository<Dispute>().GetByIdAsync(request.DisputeId, cancellationToken);
        if (dispute == null)
            return Result<ResolveDisputeResponse>.Failure("Dispute not found.");

        if (dispute.Status != DisputeStatus.Open && dispute.Status != DisputeStatus.UnderReview)
            return Result<ResolveDisputeResponse>.Failure("This dispute has already been resolved.");

        var order = await _unitOfWork.Orders.GetByIdWithAllAsync(dispute.OrderId, cancellationToken);
        if (order == null || order.Merchant == null)
            return Result<ResolveDisputeResponse>.Failure("Order or Merchant data is missing.");

        ResolveDisputeResponse response;

        if (request.Resolution.Equals("refund", StringComparison.OrdinalIgnoreCase))
        {
            var now = _dateTimeProvider.UtcNow;
            var resolutionNote = request.AdminNotes ?? "Refund issued to buyer.";
            dispute.ResolveAsRefund(resolutionNote, request.ResolvedByUserId!, now);

            var refundAmount = order.EscrowTransaction?.Amount ?? order.Price;
            order.Refund(refundAmount, now);
            
            if (order.EscrowTransaction != null)
            {
                order.EscrowTransaction.MarkAsRefunded(now);
            }
            response = new ResolveDisputeResponse("Refunded", "Dispute resolved. Buyer will be refunded.");
        }
        else // release
        {
            var now = _dateTimeProvider.UtcNow;
            var resolutionNote = request.AdminNotes ?? "Funds released to merchant.";
            dispute.ResolveAsRelease(resolutionNote, request.ResolvedByUserId!, now);

            var totalAmount = order.EscrowTransaction?.Amount ?? order.Price;
            var commissionRate = order.Merchant.CommissionRate;
            var platformCommission = totalAmount * commissionRate;
            var merchantAmount = totalAmount - platformCommission;

            var payoutSplit = new PayoutSplit
            {
                OrderId = order.Id,
                TotalAmount = totalAmount,
                MerchantAmount = merchantAmount,
                PlatformCommission = platformCommission,
                CommissionRate = commissionRate
            };
            _unitOfWork.Repository<PayoutSplit>().Add(payoutSplit);

            order.ReleaseFunds(merchantAmount, platformCommission, now);

            if (order.EscrowTransaction != null)
            {
                order.EscrowTransaction.MarkAsReleased(now);
            }
            response = new ResolveDisputeResponse("Released", "Dispute resolved. Funds released to merchant.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ResolveDisputeResponse>.Success(response);
    }
}
