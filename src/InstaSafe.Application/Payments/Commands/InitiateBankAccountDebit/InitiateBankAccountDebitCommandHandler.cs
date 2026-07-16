using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using MediatR;

namespace InstaSafe.Application.Payments.Commands.InitiateBankAccountDebit;

public class InitiateBankAccountDebitCommandHandler : IRequestHandler<InitiateBankAccountDebitCommand, Result<BankAccountDebitResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public InitiateBankAccountDebitCommandHandler(IUnitOfWork unitOfWork, IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<BankAccountDebitResponse>> Handle(InitiateBankAccountDebitCommand request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetByIdForPaymentAsync(request.OrderId, cancellationToken);

        if (order == null)
            return Result<BankAccountDebitResponse>.Failure("Order not found.");

        if (order.Status != OrderStatus.Draft)
            return Result<BankAccountDebitResponse>.Failure("Payment can only be initiated for orders in Draft status.");

        var buyer = await _unitOfWork.Buyers.GetOrCreateAsync(
            request.BuyerEmail, request.BuyerFirstName, request.BuyerLastName, request.BuyerPhone, cancellationToken);

        var transactionReference = $"DB-{order.OrderReference}-{_dateTimeProvider.UtcNow:yyyyMMddHHmmss}";

        var escrowTransaction = new EscrowTransaction
        {
            OrderId = order.Id,
            TransactionReference = transactionReference,
            Channel = PaymentChannel.BankAccount,
            Amount = order.Price
        };

        _unitOfWork.EscrowTransactions.Add(escrowTransaction);

        order.SetBuyer(buyer.Id);
        order.MarkAsPendingPayment();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<BankAccountDebitResponse>.Success(new BankAccountDebitResponse(
            transactionReference,
            $"An OTP has been sent to {request.BuyerPhone.Substring(0, 5)}*****. Enter the OTP to complete payment."));
    }
}
