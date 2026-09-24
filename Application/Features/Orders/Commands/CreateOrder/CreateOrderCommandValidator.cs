using FluentValidation;

namespace InstaSafe.Application.Features.Orders.Commands.CreateOrder;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.VendorPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.CustomerPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.DeliveryAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.AmountNgn).GreaterThan(0);
        RuleFor(x => x.BuyerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.Items).NotEmpty();
        RuleFor(x => x.Fulfillment).IsInEnum();
        RuleFor(x => x.DeliveryFeeNgn).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DriverPhone).MaximumLength(20);
        When(x => x.Fulfillment == Domain.Enums.FulfillmentType.Digital, () =>
        {
            RuleFor(x => x.DeliveryFeeNgn).Equal(0).WithMessage("Digital orders cannot have a delivery fee.");
        });
    }
}
