using FluentValidation;

namespace InstaSafe.Application.Features.Orders.Commands.ConfirmSatisfaction;

public class ConfirmSatisfactionCommandValidator : AbstractValidator<ConfirmSatisfactionCommand>
{
    public ConfirmSatisfactionCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
