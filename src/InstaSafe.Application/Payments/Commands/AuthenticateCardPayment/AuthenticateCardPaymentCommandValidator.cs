using FluentValidation;

namespace InstaSafe.Application.Payments.Commands.AuthenticateCardPayment;

public class AuthenticateCardPaymentCommandValidator : AbstractValidator<AuthenticateCardPaymentCommand>
{
    public AuthenticateCardPaymentCommandValidator()
    {
        RuleFor(v => v.TransactionId).NotEmpty();
        RuleFor(v => v.Otp).NotEmpty();
    }
}
