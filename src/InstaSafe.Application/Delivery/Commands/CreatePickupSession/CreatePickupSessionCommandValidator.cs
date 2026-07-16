using FluentValidation;

namespace InstaSafe.Application.Delivery.Commands.CreatePickupSession;

public class CreatePickupSessionCommandValidator : AbstractValidator<CreatePickupSessionCommand>
{
    public CreatePickupSessionCommandValidator()
    {
        RuleFor(v => v.OrderId)
            .NotEmpty().WithMessage("Order ID is required.");

        RuleFor(v => v.MerchantQrToken)
            .NotEmpty().WithMessage("Merchant QR token is required.");

        RuleFor(v => v.DeviceFingerprint)
            .NotEmpty().WithMessage("Device fingerprint is required.");
    }
}
