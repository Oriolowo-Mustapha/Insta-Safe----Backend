using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Models.AlatPay;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using MediatR;

namespace InstaSafe.Application.Payments.Commands.InitiateCardPayment;

public class InitiateCardPaymentCommandHandler : IRequestHandler<InitiateCardPaymentCommand, Result<CardPaymentInitiationResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAlatPayClient _alatPayClient;

    public InitiateCardPaymentCommandHandler(IUnitOfWork unitOfWork, IAlatPayClient alatPayClient)
    {
        _unitOfWork = unitOfWork;
        _alatPayClient = alatPayClient;
    }

    public async Task<Result<CardPaymentInitiationResponse>> Handle(InitiateCardPaymentCommand request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetByIdWithMerchantAsync(request.OrderId, cancellationToken);

        if (order == null)
            return Result<CardPaymentInitiationResponse>.Failure("Order not found.");

        if (order.Status != OrderStatus.Draft)
            return Result<CardPaymentInitiationResponse>.Failure("Card payment can only be initiated for orders in Draft status.");

        if (string.IsNullOrEmpty(order.Merchant.AlatPayBusinessId))
            return Result<CardPaymentInitiationResponse>.Failure("Merchant ALATPay business ID is not configured.");

        var alatPayRequest = new CardPaymentRequest(
            order.Merchant.AlatPayBusinessId,
            order.Price,
            "NGN",
            order.OrderReference
        );

        CardPaymentResponse alatPayResponse;
        try
        {
            alatPayResponse = await _alatPayClient.InitiateCardPaymentAsync(alatPayRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<CardPaymentInitiationResponse>.Failure($"Card payment initiation failed: {ex.Message}");
        }

        var buyer = await _unitOfWork.Buyers.GetOrCreateAsync(
            request.BuyerEmail, request.BuyerFirstName, request.BuyerLastName, request.BuyerPhone, cancellationToken);

        var escrowTransaction = new EscrowTransaction
        {
            OrderId = order.Id,
            AlatPayTransactionId = alatPayResponse.TransactionId,
            Channel = PaymentChannel.Card,
            Amount = order.Price
        };

        _unitOfWork.EscrowTransactions.Add(escrowTransaction);

        order.SetBuyer(buyer.Id);
        order.MarkAsPendingPayment();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<CardPaymentInitiationResponse>.Success(new CardPaymentInitiationResponse(alatPayResponse.TransactionId));
    }
}
