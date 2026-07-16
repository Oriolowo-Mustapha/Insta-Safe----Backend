using FluentValidation;

namespace InstaSafe.Application.Delivery.Commands.GenerateDeliveryQrCodes;

public class GenerateDeliveryQrCodesCommandValidator : AbstractValidator<GenerateDeliveryQrCodesCommand>
{
    public GenerateDeliveryQrCodesCommandValidator()
    {
        RuleFor(v => v.OrderId)
            .NotEmpty().WithMessage("Order ID is required.");
    }
}
