using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Models.Monnify;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using MediatR;
using Microsoft.Extensions.Options;

namespace InstaSafe.Application.Orders.Commands.GenerateEscrowLink;

public class GenerateEscrowLinkCommandHandler : IRequestHandler<GenerateEscrowLinkCommand, Result<EscrowLinkResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMonnifyPaymentService _monnifyClient;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly MonnifyOptions _options;

    public GenerateEscrowLinkCommandHandler(
        IUnitOfWork unitOfWork,
        IMonnifyPaymentService monnifyClient,
        IDateTimeProvider dateTimeProvider,
        IOptions<MonnifyOptions> options)
    {
        _unitOfWork = unitOfWork;
        _monnifyClient = monnifyClient;
        _dateTimeProvider = dateTimeProvider;
        _options = options.Value;
    }

    public async Task<Result<EscrowLinkResponse>> Handle(GenerateEscrowLinkCommand request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetByIdWithMerchantAsync(request.OrderId, cancellationToken);

        if (order == null)
            return Result<EscrowLinkResponse>.Failure("Order not found.");

        if (order.Status != OrderStatus.Draft)
            return Result<EscrowLinkResponse>.Failure("Escrow link can only be generated for orders in Draft status.");

        var buyer = await _unitOfWork.Buyers.GetOrCreateAsync(
            request.BuyerEmail, request.BuyerFirstName, request.BuyerLastName, request.BuyerPhone, cancellationToken);

        // Ensure merchant has a sub-account
        if (string.IsNullOrEmpty(order.Merchant.MonnifySubAccountCode))
        {
            if (string.IsNullOrEmpty(order.Merchant.PayoutBankAccount) || string.IsNullOrEmpty(order.Merchant.PayoutBankCode))
                return Result<EscrowLinkResponse>.Failure("Merchant must have a payout bank account configured to receive funds.");

            var subAccountReq = new SubAccountRequest(
                "NGN",
                order.Merchant.PayoutBankAccount,
                order.Merchant.PayoutBankCode,
                order.Merchant.Email,
                98.0m // 98% to merchant, 2% platform fee
            );

            try
            {
                var subResponse = await _monnifyClient.CreateSubAccountAsync(subAccountReq, cancellationToken);
                order.Merchant.MonnifySubAccountCode = subResponse.ResponseBody.SubAccountCode;
                // We'll save this when we save the order
            }
            catch (Exception ex)
            {
                return Result<EscrowLinkResponse>.Failure($"Failed to create merchant sub-account: {ex.Message}");
            }
        }

        var initReq = new InitTransactionRequest(
            order.Price,
            $"{request.BuyerFirstName} {request.BuyerLastName}",
            request.BuyerEmail,
            order.OrderReference,
            $"InstaSafe Escrow: {order.ItemName}",
            "NGN",
            _options.ContractCode,
            "https://instasafe.ng/payment-success", // Replace with actual redirect
            new[] { "CARD", "ACCOUNT_TRANSFER", "USSD" },
            new[]
            {
                new IncomeSplitConfig(
                    order.Merchant.MonnifySubAccountCode,
                    98.0m, // Ensure 98% split
                    0,
                    true // Fee bearer
                )
            }
        );

        MonnifyBaseResponse<InitTransactionResponse> initResponse;
        try
        {
            initResponse = await _monnifyClient.InitializeTransactionAsync(initReq, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<EscrowLinkResponse>.Failure($"Failed to generate payment link: {ex.Message}");
        }

        if (initResponse.ResponseBody == null || string.IsNullOrEmpty(initResponse.ResponseBody.CheckoutUrl))
            return Result<EscrowLinkResponse>.Failure("Monnify did not return a valid checkout URL.");

        var escrowTransaction = new EscrowTransaction
        {
            OrderId = order.Id,
            MonnifyTransactionReference = initResponse.ResponseBody.TransactionReference,
            CheckoutUrl = initResponse.ResponseBody.CheckoutUrl,
            TransactionReference = order.OrderReference,
            Channel = PaymentChannel.Card, // Or multi
            Amount = order.Price
        };

        _unitOfWork.EscrowTransactions.Add(escrowTransaction);

        order.SetBuyer(buyer.Id);
        order.GenerateEscrowLink(initResponse.ResponseBody.CheckoutUrl, _dateTimeProvider.UtcNow.AddMinutes(60));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // We don't have a virtual bank account number yet, so we return the checkout URL instead
        // For backwards compatibility with the response model, we can return empty or update the response model
        return Result<EscrowLinkResponse>.Success(new EscrowLinkResponse(
            order.Id,
            string.Empty,
            string.Empty,
            _dateTimeProvider.UtcNow.AddMinutes(60)
        ));
    }
}
