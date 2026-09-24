using FluentValidation;

namespace InstaSafe.Application.Features.Dispatch.Commands.ConfirmDelivery;

public class ConfirmDeliveryCommandValidator : AbstractValidator<ConfirmDeliveryCommand>
{
    public ConfirmDeliveryCommandValidator()
    {
        RuleFor(x => x.DispatcherId).NotEmpty();
        RuleFor(x => x.DriverPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Otp).NotEmpty().MinimumLength(4).MaximumLength(10);
    }
}
