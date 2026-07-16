using FluentValidation;

namespace InstaSafe.Application.Delivery.Commands.ConfirmDeliveryScan;

public class ConfirmDeliveryScanCommandValidator : AbstractValidator<ConfirmDeliveryScanCommand>
{
    public ConfirmDeliveryScanCommandValidator()
    {
        RuleFor(v => v.OrderId)
            .NotEmpty().WithMessage("Order ID is required.");

        RuleFor(v => v.SessionId)
            .NotEmpty().WithMessage("Session ID is required.");

        RuleFor(v => v.BuyerQrToken)
            .NotEmpty().WithMessage("Buyer QR token is required.");

        RuleFor(v => v.DeviceFingerprint)
            .NotEmpty().WithMessage("Device fingerprint is required.");
    }
}
