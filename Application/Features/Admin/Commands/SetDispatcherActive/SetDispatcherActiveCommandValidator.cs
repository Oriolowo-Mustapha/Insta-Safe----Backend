using FluentValidation;

namespace InstaSafe.Application.Features.Admin.Commands.SetDispatcherActive;

public class SetDispatcherActiveCommandValidator : AbstractValidator<SetDispatcherActiveCommand>
{
    public SetDispatcherActiveCommandValidator()
    {
        RuleFor(x => x.DispatcherId).NotEmpty();
    }
}
