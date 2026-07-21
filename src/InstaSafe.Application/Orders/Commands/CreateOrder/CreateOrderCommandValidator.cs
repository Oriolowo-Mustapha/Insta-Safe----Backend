using FluentValidation;

namespace InstaSafe.Application.Orders.Commands.CreateOrder;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(v => v.MerchantId)
            .NotEmpty().WithMessage("Merchant ID is required.");

        RuleFor(v => v.ItemName)
            .NotEmpty().WithMessage("Item name is required.")
            .MaximumLength(500);

        RuleFor(v => v.ItemDescription)
            .MaximumLength(2000);

        RuleFor(v => v.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero.");

        RuleFor(v => v.BuyerFirstName)
            .NotEmpty().WithMessage("Buyer first name is required.");

        RuleFor(v => v.BuyerLastName)
            .NotEmpty().WithMessage("Buyer last name is required.");

        RuleFor(v => v.BuyerEmail)
            .NotEmpty().WithMessage("Buyer email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(v => v.BuyerPhone)
            .NotEmpty().WithMessage("Buyer phone is required.")
            .Matches(@"^\+[1-9]\d{10,14}$").WithMessage("Phone number must include country code (e.g., +234...).");

        RuleFor(v => v.DispatcherPhone)
            .Matches(@"^\+[1-9]\d{10,14}$").WithMessage("Phone number must include country code (e.g., +234...).")
            .When(v => !string.IsNullOrEmpty(v.DispatcherPhone));
    }
}
