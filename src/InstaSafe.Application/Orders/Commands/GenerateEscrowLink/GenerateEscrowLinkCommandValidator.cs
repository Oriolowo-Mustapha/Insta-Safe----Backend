using FluentValidation;

namespace InstaSafe.Application.Orders.Commands.GenerateEscrowLink;

public class GenerateEscrowLinkCommandValidator : AbstractValidator<GenerateEscrowLinkCommand>
{
    public GenerateEscrowLinkCommandValidator()
    {
        RuleFor(v => v.OrderId)
            .NotEmpty().WithMessage("Order ID is required.");

        RuleFor(v => v.BuyerEmail)
            .NotEmpty().WithMessage("Buyer email is required.")
            .EmailAddress().WithMessage("A valid email is required.");

        RuleFor(v => v.BuyerFirstName)
            .NotEmpty().WithMessage("Buyer first name is required.")
            .MaximumLength(100);

        RuleFor(v => v.BuyerLastName)
            .NotEmpty().WithMessage("Buyer last name is required.")
            .MaximumLength(100);

        RuleFor(v => v.BuyerPhone)
            .NotEmpty().WithMessage("Buyer phone is required.");
    }
}
