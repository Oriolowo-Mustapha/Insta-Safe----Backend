using FluentValidation;

namespace InstaSafe.Application.Features.Dispatch.Commands.VerifyDispatcherOtp;

public class VerifyDispatcherOtpCommandValidator : AbstractValidator<VerifyDispatcherOtpCommand>
{
    public VerifyDispatcherOtpCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Code).NotEmpty().MinimumLength(4).MaximumLength(10);
    }
}
