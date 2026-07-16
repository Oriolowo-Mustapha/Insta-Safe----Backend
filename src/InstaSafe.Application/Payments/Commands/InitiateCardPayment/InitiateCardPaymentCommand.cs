using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Payments.Commands.InitiateCardPayment;

public record InitiateCardPaymentCommand(
    Guid OrderId,
    string BuyerFirstName,
    string BuyerLastName,
    string BuyerEmail,
    string BuyerPhone
) : IRequest<Result<CardPaymentInitiationResponse>>;

public record CardPaymentInitiationResponse(string TransactionId);
