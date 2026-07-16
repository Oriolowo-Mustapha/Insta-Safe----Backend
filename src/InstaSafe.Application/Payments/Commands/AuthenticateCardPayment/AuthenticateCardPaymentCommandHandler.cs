using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Payments.Commands.AuthenticateCardPayment;

public class AuthenticateCardPaymentCommandHandler : IRequestHandler<AuthenticateCardPaymentCommand, Result<string>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAlatPayClient _alatPayClient;

    public AuthenticateCardPaymentCommandHandler(IUnitOfWork unitOfWork, IAlatPayClient alatPayClient)
    {
        _unitOfWork = unitOfWork;
        _alatPayClient = alatPayClient;
    }

    public async Task<Result<string>> Handle(AuthenticateCardPaymentCommand request, CancellationToken cancellationToken)
    {
        var escrowTx = await _unitOfWork.EscrowTransactions.GetByAlatPayTransactionIdAsync(request.TransactionId, cancellationToken);

        if (escrowTx == null)
            return Result<string>.Failure("Transaction not found.");

        var status = await _alatPayClient.CheckTransactionStatusAsync(
            "card", request.TransactionId, cancellationToken);

        if (status.Status.Equals("successful", StringComparison.OrdinalIgnoreCase))
        {
            var now = DateTime.UtcNow;
            escrowTx.MarkAsFunded(now);
            escrowTx.Order.MarkAsFunded(now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<string>.Success("Card payment authenticated successfully.");
        }

        return Result<string>.Failure("Card payment authentication failed.");
    }
}
