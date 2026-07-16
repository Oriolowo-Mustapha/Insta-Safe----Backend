namespace InstaSafe.Application.Common.Models.AlatPay;

public record VirtualAccountRequest(
    string BusinessId,
    string BusinessName,
    decimal Amount,
    string Currency,
    string OrderId,
    string Description,
    string CustomerEmail,
    string CustomerPhone,
    string CustomerFirstName,
    string CustomerLastName
);

public record VirtualAccountResponse(
    string VirtualBankAccountNumber,
    string VirtualBankCode,
    string TransactionId,
    string Status,
    DateTime? ExpiredAt
);

public record CardPaymentRequest(
    string BusinessId,
    decimal Amount,
    string Currency,
    string OrderId
);

public record CardPaymentResponse(
    string TransactionId,
    string Status
);

public record TransactionStatusResponse(
    string Status,
    decimal Amount
);
