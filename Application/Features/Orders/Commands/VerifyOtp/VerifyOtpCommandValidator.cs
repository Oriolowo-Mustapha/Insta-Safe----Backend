using FluentValidation;

namespace InstaSafe.Application.Features.Orders.Commands.VerifyOtp;

public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Otp).NotEmpty().Length(4, 8);
    }
}
