using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Models.Monnify;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InstaSafe.Application.Payouts.Commands.ExecuteSplitPayout;

public class ExecuteSplitPayoutCommandHandler : IRequestHandler<ExecuteSplitPayoutCommand, Result<ExecuteSplitPayoutResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMonnifyPaymentService _monnifyClient;
    private readonly MonnifyOptions _options;
    private readonly ILogger<ExecuteSplitPayoutCommandHandler> _logger;

    public ExecuteSplitPayoutCommandHandler(
        IUnitOfWork unitOfWork, 
        IDateTimeProvider dateTimeProvider,
        IMonnifyPaymentService monnifyClient,
        IOptions<MonnifyOptions> options,
        ILogger<ExecuteSplitPayoutCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _monnifyClient = monnifyClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<ExecuteSplitPayoutResponse>> Handle(ExecuteSplitPayoutCommand request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetByIdWithAllAsync(request.OrderId, cancellationToken);
        if (order == null)
            return Result<ExecuteSplitPayoutResponse>.Failure("Order not found.");

        if (order.Status != OrderStatus.CompletedReleased)
            return Result<ExecuteSplitPayoutResponse>.Failure("Order must be CompletedReleased to execute payout.");

        var payoutSplit = order.PayoutSplit;

        if (payoutSplit != null && payoutSplit.Status == PayoutStatus.Completed)
            return Result<ExecuteSplitPayoutResponse>.Failure("Payout already executed.");

        if (payoutSplit == null)
        {
            var totalAmount = order.EscrowTransaction?.Amount ?? order.Price;
            var commissionRate = order.Merchant!.CommissionRate;
            var platformCommission = totalAmount * commissionRate;
            var merchantAmount = totalAmount - platformCommission;

            payoutSplit = new PayoutSplit
            {
                OrderId = order.Id,
                TotalAmount = totalAmount,
                MerchantAmount = merchantAmount,
                PlatformCommission = platformCommission,
                CommissionRate = commissionRate
            };
            
            _unitOfWork.Repository<PayoutSplit>().Add(payoutSplit);
        }

        payoutSplit.MarkAsProcessing();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Disburse funds to Merchant via Single Transfer
        if (string.IsNullOrEmpty(order.Merchant!.PayoutBankAccount) || string.IsNullOrEmpty(order.Merchant.PayoutBankCode))
        {
            payoutSplit.MarkAsFailed();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<ExecuteSplitPayoutResponse>.Failure("Merchant does not have a payout bank account configured.");
        }

        var transferRef = $"PAYOUT-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        var transferReq = new SingleTransferRequest(
            payoutSplit.MerchantAmount,
            transferRef,
            $"InstaSafe Payout: Order {order.OrderReference}",
            order.Merchant.PayoutBankCode,
            order.Merchant.PayoutBankAccount,
            order.Merchant.BusinessName,
            "NGN",
            _options.WalletAccountNumber
        );

        try
        {
            var response = await _monnifyClient.InitiateSingleTransferAsync(transferReq, cancellationToken);
            
            if (response.RequestSuccessful)
            {
                payoutSplit.MarkAsCompleted(response.ResponseBody.Reference, _dateTimeProvider.UtcNow);
                _logger.LogInformation("Successfully initiated single transfer for payout {PayoutId}. Ref: {Ref}", payoutSplit.Id, transferRef);
            }
            else
            {
                payoutSplit.MarkAsFailed();
                _logger.LogWarning("Single transfer failed for payout {PayoutId}. Message: {Msg}", payoutSplit.Id, response.ResponseMessage);
            }
        }
        catch (Exception ex)
        {
            payoutSplit.MarkAsFailed();
            _logger.LogError(ex, "Exception while initiating single transfer for payout {PayoutId}", payoutSplit.Id);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ExecuteSplitPayoutResponse>.Success(
            new ExecuteSplitPayoutResponse(
                payoutSplit.TotalAmount,
                payoutSplit.MerchantAmount,
                payoutSplit.PlatformCommission,
                payoutSplit.Status.ToString()));
    }
}
