using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using MediatR;

namespace InstaSafe.Application.Payouts.Commands.ExecuteSplitPayout;

public class ExecuteSplitPayoutCommandHandler : IRequestHandler<ExecuteSplitPayoutCommand, Result<ExecuteSplitPayoutResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ExecuteSplitPayoutCommandHandler(IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
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

        // TODO: Call ALATPay payout API here
        payoutSplit.MarkAsCompleted($"PAYOUT-{Guid.NewGuid().ToString()[..8].ToUpper()}", _dateTimeProvider.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<ExecuteSplitPayoutResponse>.Success(
            new ExecuteSplitPayoutResponse(
                payoutSplit.TotalAmount,
                payoutSplit.MerchantAmount,
                payoutSplit.PlatformCommission,
                payoutSplit.Status.ToString()));
    }
}
