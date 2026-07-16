using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Payments.Commands.AuthenticateCardPayment;

public record AuthenticateCardPaymentCommand(
    string TransactionId,
    string Otp
) : IRequest<Result<string>>;
