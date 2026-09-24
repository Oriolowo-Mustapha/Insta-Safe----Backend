using FluentValidation;

namespace InstaSafe.Application.Features.Auth.Commands.RequestVendorOtp;

public class RequestVendorOtpCommandValidator : AbstractValidator<RequestVendorOtpCommand>
{
    public RequestVendorOtpCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}
