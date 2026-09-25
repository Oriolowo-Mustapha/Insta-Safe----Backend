using FluentValidation;

namespace InstaSafe.Application.Features.Auth.Commands.RequestEmailCode;

public class RequestEmailCodeCommandValidator : AbstractValidator<RequestEmailCodeCommand>
{
    public RequestEmailCodeCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(200).EmailAddress();
    }
}
