using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Payments.Commands.InitiateBankAccountDebit;

public record InitiateBankAccountDebitCommand(
    Guid OrderId,
    string BuyerFirstName,
    string BuyerLastName,
    string BuyerEmail,
    string BuyerPhone
) : IRequest<Result<BankAccountDebitResponse>>;

public record BankAccountDebitResponse(string TransactionReference, string OtpMessage);
