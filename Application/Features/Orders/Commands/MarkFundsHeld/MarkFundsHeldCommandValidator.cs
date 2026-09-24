using FluentValidation;

namespace InstaSafe.Application.Features.Orders.Commands.MarkFundsHeld;

public class MarkFundsHeldCommandValidator : AbstractValidator<MarkFundsHeldCommand>
{
    public MarkFundsHeldCommandValidator() =>
        RuleFor(x => x.PaystackReference).NotEmpty().MaximumLength(100);
}
