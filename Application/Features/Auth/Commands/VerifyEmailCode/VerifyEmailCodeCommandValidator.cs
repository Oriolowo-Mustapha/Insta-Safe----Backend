using FluentValidation;

namespace InstaSafe.Application.Features.Auth.Commands.VerifyEmailCode;

public class VerifyEmailCodeCommandValidator : AbstractValidator<VerifyEmailCodeCommand>
{
    public VerifyEmailCodeCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(200).EmailAddress();
        RuleFor(x => x.Code).NotEmpty().MinimumLength(4).MaximumLength(10);
    }
}
