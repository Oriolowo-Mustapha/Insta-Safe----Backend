using FluentValidation;

namespace InstaSafe.Application.Features.Admin.Commands.RetryRiderPayout;

public class RetryRiderPayoutCommandValidator : AbstractValidator<RetryRiderPayoutCommand>
{
    public RetryRiderPayoutCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
