using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Models.AlatPay;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using MediatR;

namespace InstaSafe.Application.Orders.Commands.GenerateEscrowLink;

public class GenerateEscrowLinkCommandHandler : IRequestHandler<GenerateEscrowLinkCommand, Result<EscrowLinkResponse>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAlatPayClient _alatPayClient;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GenerateEscrowLinkCommandHandler(
        IUnitOfWork unitOfWork,
        IAlatPayClient alatPayClient,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _alatPayClient = alatPayClient;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<EscrowLinkResponse>> Handle(GenerateEscrowLinkCommand request, CancellationToken cancellationToken)
    {
        var order = await _unitOfWork.Orders.GetByIdWithMerchantAsync(request.OrderId, cancellationToken);

        if (order == null)
            return Result<EscrowLinkResponse>.Failure("Order not found.");

        if (order.Status != OrderStatus.Draft)
            return Result<EscrowLinkResponse>.Failure("Escrow link can only be generated for orders in Draft status.");

        if (string.IsNullOrEmpty(order.Merchant.AlatPayBusinessId))
            return Result<EscrowLinkResponse>.Failure("Merchant ALATPay business ID is not configured.");

        var buyer = await _unitOfWork.Buyers.GetOrCreateAsync(
            request.BuyerEmail, request.BuyerFirstName, request.BuyerLastName, request.BuyerPhone, cancellationToken);

        var alatPayRequest = new VirtualAccountRequest(
            order.Merchant.AlatPayBusinessId,
            order.Merchant.BusinessName,
            order.Price,
            "NGN",
            order.OrderReference,
            $"InstaSafe Escrow: {order.ItemName}",
            request.BuyerEmail,
            request.BuyerPhone,
            request.BuyerFirstName,
            request.BuyerLastName
        );

        VirtualAccountResponse alatPayResponse;
        try
        {
            alatPayResponse = await _alatPayClient.GenerateVirtualAccountAsync(alatPayRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<EscrowLinkResponse>.Failure($"Failed to generate virtual account: {ex.Message}");
        }

        if (string.IsNullOrEmpty(alatPayResponse.VirtualBankAccountNumber))
            return Result<EscrowLinkResponse>.Failure("ALATPay did not return a virtual account number.");

        var escrowTransaction = new EscrowTransaction
        {
            OrderId = order.Id,
            AlatPayTransactionId = alatPayResponse.TransactionId,
            Channel = PaymentChannel.BankTransfer,
            Amount = order.Price,
            VirtualAccountNumber = alatPayResponse.VirtualBankAccountNumber,
            VirtualBankCode = alatPayResponse.VirtualBankCode,
            VirtualAccountExpiresAt = alatPayResponse.ExpiredAt ?? _dateTimeProvider.UtcNow.AddMinutes(30)
        };

        _unitOfWork.EscrowTransactions.Add(escrowTransaction);

        order.SetBuyer(buyer.Id);
        order.GenerateEscrowLink($"/pay/{order.Id}", alatPayResponse.ExpiredAt ?? _dateTimeProvider.UtcNow.AddMinutes(30));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<EscrowLinkResponse>.Success(new EscrowLinkResponse(
            order.Id,
            alatPayResponse.VirtualBankAccountNumber,
            alatPayResponse.VirtualBankCode,
            alatPayResponse.ExpiredAt ?? _dateTimeProvider.UtcNow.AddMinutes(30)
        ));
    }
}
