using FluentValidation;

namespace InstaSafe.Application.Features.Dispatch.Commands.RequestDispatcherOtp;

public class RequestDispatcherOtpCommandValidator : AbstractValidator<RequestDispatcherOtpCommand>
{
    public RequestDispatcherOtpCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}
