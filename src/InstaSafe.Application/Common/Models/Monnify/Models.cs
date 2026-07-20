namespace InstaSafe.Application.Common.Models.Monnify;

public record MonnifyAuthResponse(string AccessToken, int ExpiresIn);

public record SubAccountRequest(
    string CurrencyCode,
    string AccountNumber,
    string BankCode,
    string Email,
    decimal DefaultSplitPercentage);

public record SubAccountResponse(
    string SubAccountCode,
    string AccountNumber,
    string AccountName,
    string CurrencyCode,
    string Email,
    string BankCode,
    string BankName,
    decimal DefaultSplitPercentage);

public record IncomeSplitConfig(
    string SubAccountCode,
    decimal FeePercentage,
    decimal SplitAmount,
    bool FeeBearer);

public record InitTransactionRequest(
    decimal Amount,
    string CustomerName,
    string CustomerEmail,
    string PaymentReference,
    string PaymentDescription,
    string CurrencyCode,
    string ContractCode,
    string RedirectUrl,
    string[] PaymentMethods,
    IncomeSplitConfig[]? IncomeSplitConfig);

public record InitTransactionResponse(
    string TransactionReference,
    string PaymentReference,
    string MerchantName,
    string ApiKey,
    string[] EnabledPaymentMethod,
    string CheckoutUrl);

public record SingleTransferRequest(
    decimal Amount,
    string Reference,
    string Narration,
    string DestinationBankCode,
    string DestinationAccountNumber,
    string Currency,
    string SourceAccountNumber);

public record SingleTransferResponse(
    decimal Amount,
    string Reference,
    string Status,
    string DateCreated,
    decimal TotalFee,
    string DestinationAccountName,
    string DestinationBankName,
    string DestinationAccountNumber,
    string DestinationBankCode);

public record RefundRequest(
    string TransactionReference,
    decimal RefundAmount,
    string RefundReference,
    string RefundReason,
    string CustomerNote,
    string DestinationAccountNumber,
    string DestinationAccountBankCode);

public record RefundResponse(
    string RefundReference,
    string Reference,
    string TransactionReference,
    string RefundReason,
    string CustomerNote,
    decimal RefundAmount,
    string RefundType,
    string RefundStatus,
    string RefundStrategy,
    string Comment,
    string CreatedOn);

public record MonnifyBaseResponse<T>(
    bool RequestSuccessful,
    string ResponseMessage,
    string ResponseCode,
    T ResponseBody);

// Verification APIs
public record BvnMatchRequest(
    string Bvn,
    string Name,
    string? DateOfBirth = null,
    string? MobileNo = null);

public record BvnMatchDetails(
    string MatchStatus,
    int MatchPercentage);

public record BvnMatchResponse(
    string Bvn,
    BvnMatchDetails Name,
    string? DateOfBirth,
    string? MobileNo);

public record NinVerificationRequest(string Nin);

public record NinVerificationResponse(
    string Nin,
    string LastName,
    string FirstName,
    string? MiddleName,
    string DateOfBirth,
    string Gender,
    string MobileNumber);

public record AccountVerificationResponse(
    string AccountNumber,
    string AccountName,
    string BankCode);
