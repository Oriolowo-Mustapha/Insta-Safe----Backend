using InstaSafe.Application.Common.Models.AlatPay;

namespace InstaSafe.Application.Common.Interfaces;

public interface IAlatPayClient
{
    Task<VirtualAccountResponse> GenerateVirtualAccountAsync(VirtualAccountRequest request, CancellationToken ct);
    Task<CardPaymentResponse> InitiateCardPaymentAsync(CardPaymentRequest request, CancellationToken ct);
    Task<TransactionStatusResponse> CheckTransactionStatusAsync(string channelId, string transactionReference, CancellationToken ct);
}
