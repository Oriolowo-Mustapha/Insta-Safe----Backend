using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Orders.Queries.VerifyTransactionStatus;

public class VerifyTransactionStatusQueryHandler : IRequestHandler<VerifyTransactionStatusQuery, Result<TransactionVerificationResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IAlatPayClient _alatPayClient;

    public VerifyTransactionStatusQueryHandler(IApplicationDbContext context, IAlatPayClient alatPayClient)
    {
        _context = context;
        _alatPayClient = alatPayClient;
    }

    public async Task<Result<TransactionVerificationResponse>> Handle(VerifyTransactionStatusQuery request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.EscrowTransaction)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
            return Result<TransactionVerificationResponse>.Failure("Order not found.");

        if (order.EscrowTransaction?.AlatPayTransactionId == null && order.EscrowTransaction?.TransactionReference == null)
            return Result<TransactionVerificationResponse>.Success(new TransactionVerificationResponse
            {
                Status = order.Status.ToString(),
                IsFunded = false,
                Detail = "No payment transaction found for this order."
            });

        if (order.EscrowTransaction.AlatPayTransactionId != null)
        {
            try
            {
                var status = await _alatPayClient.CheckTransactionStatusAsync(
                    "alatpay", order.EscrowTransaction.AlatPayTransactionId, cancellationToken);

                return Result<TransactionVerificationResponse>.Success(new TransactionVerificationResponse
                {
                    Status = status.Status,
                    IsFunded = status.Status.Equals("successful", StringComparison.OrdinalIgnoreCase),
                    AlatPayTransactionId = order.EscrowTransaction.AlatPayTransactionId,
                    Detail = $"ALATPay reports status: {status.Status}"
                });
            }
            catch (Exception ex)
            {
                return Result<TransactionVerificationResponse>.Success(new TransactionVerificationResponse
                {
                    Status = order.Status.ToString(),
                    IsFunded = order.Status == Domain.Enums.OrderStatus.FundedInEscrow,
                    Detail = $"Could not verify with ALATPay: {ex.Message}"
                });
            }
        }

        return Result<TransactionVerificationResponse>.Success(new TransactionVerificationResponse
        {
            Status = order.Status.ToString(),
            IsFunded = order.Status == Domain.Enums.OrderStatus.FundedInEscrow,
            Detail = "Local order status (no ALATPay verification)."
        });
    }
}
