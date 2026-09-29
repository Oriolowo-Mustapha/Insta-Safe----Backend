using FluentValidation;

namespace InstaSafe.Application.Features.Admin.Commands.ForceRelease;

public class ForceReleaseOrderCommandValidator : AbstractValidator<ForceReleaseOrderCommand>
{
    public ForceReleaseOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}
