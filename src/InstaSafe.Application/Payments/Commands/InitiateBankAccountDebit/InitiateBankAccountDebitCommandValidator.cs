using FluentValidation;

namespace InstaSafe.Application.Payments.Commands.InitiateBankAccountDebit;

public class InitiateBankAccountDebitCommandValidator : AbstractValidator<InitiateBankAccountDebitCommand>
{
    public InitiateBankAccountDebitCommandValidator()
    {
        RuleFor(v => v.OrderId).NotEmpty();
        RuleFor(v => v.BuyerEmail).NotEmpty().EmailAddress();
        RuleFor(v => v.BuyerFirstName).NotEmpty().MaximumLength(100);
        RuleFor(v => v.BuyerLastName).NotEmpty().MaximumLength(100);
        RuleFor(v => v.BuyerPhone).NotEmpty();
    }
}
