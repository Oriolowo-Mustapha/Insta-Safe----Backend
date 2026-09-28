using FluentValidation;

namespace InstaSafe.Application.Features.Admin.Commands.RetryPayout;

public class RetryPayoutCommandValidator : AbstractValidator<RetryPayoutCommand>
{
    public RetryPayoutCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
