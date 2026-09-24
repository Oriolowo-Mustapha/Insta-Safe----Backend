using FluentValidation;

namespace InstaSafe.Application.Features.Orders.Commands.RefundOrder;

public class RefundOrderCommandValidator : AbstractValidator<RefundOrderCommand>
{
    public RefundOrderCommandValidator() =>
        RuleFor(x => x.OrderId).NotEmpty();
}
