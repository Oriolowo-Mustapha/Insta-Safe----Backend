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
        RuleFor(x => x.Fulfillment)
            .Must(f => f is Domain.Enums.FulfillmentType.Dispatch or Domain.Enums.FulfillmentType.SelfDelivery)
            .WithMessage("Fulfilment must be dispatch (0) or self-delivery (2). Digital fulfilment has been disabled.")
            .WithErrorCode("fulfillment.unsupported");
        RuleFor(x => x.DeliveryFeeNgn).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DriverPhone).MaximumLength(20);

        // The vendor now states which of the two flows they are in, so each
        // shape is unambiguous. An ambiguous order (Dispatch with no rider) is
        // what made the track page unable to decide whether verify-otp would
        // 200 or 400, so it is no longer accepted.
        When(x => x.Fulfillment == Domain.Enums.FulfillmentType.Dispatch, () =>
        {
            RuleFor(x => x.DriverPhone)
                .NotEmpty()
                .WithMessage("A dispatch order needs a rider. Send fulfilment 2 (self-delivery) to deliver it yourself.")
                .WithErrorCode("fulfillment.dispatch_needs_rider");
        });

        When(x => x.Fulfillment == Domain.Enums.FulfillmentType.SelfDelivery, () =>
        {
            RuleFor(x => x.DriverPhone)
                .Must(string.IsNullOrWhiteSpace)
                .WithMessage("A self-delivery order must not name a rider. Send fulfilment 0 (dispatch) instead.")
                .WithErrorCode("fulfillment.selfdelivery_no_rider");
        });
    }
}
