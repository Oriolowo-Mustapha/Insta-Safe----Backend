using FluentValidation;

namespace InstaSafe.Application.Payments.Commands.InitiateCardPayment;

public class InitiateCardPaymentCommandValidator : AbstractValidator<InitiateCardPaymentCommand>
{
    public InitiateCardPaymentCommandValidator()
    {
        RuleFor(v => v.OrderId).NotEmpty();
        RuleFor(v => v.BuyerEmail).NotEmpty().EmailAddress();
        RuleFor(v => v.BuyerFirstName).NotEmpty().MaximumLength(100);
        RuleFor(v => v.BuyerLastName).NotEmpty().MaximumLength(100);
        RuleFor(v => v.BuyerPhone).NotEmpty();
    }
}
