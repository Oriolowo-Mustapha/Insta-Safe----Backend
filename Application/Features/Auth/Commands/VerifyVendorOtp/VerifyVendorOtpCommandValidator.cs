using FluentValidation;

namespace InstaSafe.Application.Features.Auth.Commands.VerifyVendorOtp;

public class VerifyVendorOtpCommandValidator : AbstractValidator<VerifyVendorOtpCommand>
{
    public VerifyVendorOtpCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Code).NotEmpty().MinimumLength(4).MaximumLength(10);
    }
}
