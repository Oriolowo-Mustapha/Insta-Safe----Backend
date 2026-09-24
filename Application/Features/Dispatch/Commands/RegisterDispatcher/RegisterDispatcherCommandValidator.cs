using FluentValidation;

namespace InstaSafe.Application.Features.Dispatch.Commands.RegisterDispatcher;

public class RegisterDispatcherCommandValidator : AbstractValidator<RegisterDispatcherCommand>
{
    public RegisterDispatcherCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.FirstName).MaximumLength(120);
        RuleFor(x => x.LastName).MaximumLength(120);
        RuleFor(x => x.AccountNumber).MaximumLength(20);
        RuleFor(x => x.BankCode).MaximumLength(10);
        When(x => x.AccountNumber is not null || x.BankCode is not null, () =>
        {
            RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(20);
            RuleFor(x => x.BankCode).NotEmpty().MaximumLength(10);
        });
    }
}
