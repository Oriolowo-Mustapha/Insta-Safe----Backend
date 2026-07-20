using FluentValidation;

namespace InstaSafe.Application.Orders.Commands.DeleteOrder;

public class DeleteOrderCommandValidator : AbstractValidator<DeleteOrderCommand>
{
    public DeleteOrderCommandValidator()
    {
        RuleFor(v => v.OrderId).NotEmpty().WithMessage("Order ID is required.");
        RuleFor(v => v.MerchantId).NotEmpty().WithMessage("Merchant ID is required.");
    }
}
