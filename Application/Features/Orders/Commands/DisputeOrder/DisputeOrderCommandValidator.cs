using FluentValidation;

namespace InstaSafe.Application.Features.Orders.Commands.DisputeOrder;

public class DisputeOrderCommandValidator : AbstractValidator<DisputeOrderCommand>
{
    public DisputeOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}
