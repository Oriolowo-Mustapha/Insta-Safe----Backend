using InstaSafe.Application.Common.Models.Monnify;

namespace InstaSafe.Application.Common.Interfaces;

public interface IMonnifyPaymentService
{
    Task<MonnifyBaseResponse<SubAccountResponse>> CreateSubAccountAsync(SubAccountRequest request, CancellationToken ct = default);
    Task<MonnifyBaseResponse<InitTransactionResponse>> InitializeTransactionAsync(InitTransactionRequest request, CancellationToken ct = default);
    Task<MonnifyBaseResponse<SingleTransferResponse>> InitiateSingleTransferAsync(SingleTransferRequest request, CancellationToken ct = default);
    Task<MonnifyBaseResponse<RefundResponse>> InitiateRefundAsync(RefundRequest request, CancellationToken ct = default);
    Task<MonnifyBaseResponse<BvnMatchResponse>> VerifyBvnAsync(BvnMatchRequest request, CancellationToken ct = default);
    Task<MonnifyBaseResponse<NinVerificationResponse>> VerifyNinAsync(NinVerificationRequest request, CancellationToken ct = default);
    Task<MonnifyBaseResponse<AccountVerificationResponse>> VerifyAccountAsync(string accountNumber, string bankCode, CancellationToken ct = default);
    Task<MonnifyBaseResponse<List<BankResponse>>> GetBanksAsync(CancellationToken ct = default);
}
