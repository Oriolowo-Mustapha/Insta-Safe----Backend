using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Models.Monnify;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Disputes.Commands.ResolveDispute;

public class ResolveDisputeCommandHandler : IRequestHandler<ResolveDisputeCommand, Result<ResolveDisputeResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMonnifyPaymentService _monnifyClient;
    private readonly ILogger<ResolveDisputeCommandHandler> _logger;

    public ResolveDisputeCommandHandler(
        IUnitOfWork unitOfWork, 
        IDateTimeProvider dateTimeProvider,
        IMonnifyPaymentService monnifyClient,
        ILogger<ResolveDisputeCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _monnifyClient = monnifyClient;
        _logger = logger;
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
            var refundAmount = order.EscrowTransaction?.Amount ?? order.Price;
            var resolutionNote = request.AdminNotes ?? "Refund issued to buyer.";

            if (order.EscrowTransaction == null || string.IsNullOrEmpty(order.EscrowTransaction.MonnifyTransactionReference))
            {
                return Result<ResolveDisputeResponse>.Failure("Cannot process refund: Monnify transaction reference is missing.");
            }

            var refundRef = $"REFUND-ORD-{order.OrderReference}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            var refundReq = new RefundRequest(
                order.EscrowTransaction.MonnifyTransactionReference,
                refundAmount,
                refundRef,
                resolutionNote,
                "Full refund for dispute resolution"
            );

            try
            {
                var monnifyResponse = await _monnifyClient.InitiateRefundAsync(refundReq, cancellationToken);
                if (monnifyResponse.RequestSuccessful)
                {
                    _logger.LogInformation("Successfully initiated refund {RefundRef} for Order {OrderId}", refundRef, order.Id);
                }
                else
                {
                    _logger.LogWarning("Monnify refund failed for Order {OrderId}. Message: {Msg}", order.Id, monnifyResponse.ResponseMessage);
                    return Result<ResolveDisputeResponse>.Failure($"Monnify refund failed: {monnifyResponse.ResponseMessage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception initiating refund for Order {OrderId}", order.Id);
                return Result<ResolveDisputeResponse>.Failure($"Exception initiating refund: {ex.Message}");
            }

            dispute.ResolveAsRefund(resolutionNote, request.ResolvedByUserId!, now);
            order.Refund(refundAmount, now);
            order.EscrowTransaction.MarkAsRefunded(now);
            
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
            response = new ResolveDisputeResponse("Released", "Dispute resolved. Funds released to merchant. Payout job will disburse funds.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<ResolveDisputeResponse>.Success(response);
    }
}
