using FluentValidation;

namespace InstaSafe.Application.Features.Auth.Commands.VendorLogin;

public class VendorLoginCommandValidator : AbstractValidator<VendorLoginCommand>
{
    public VendorLoginCommandValidator()
    {
        RuleFor(x => x.LoginId).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(100);
    }
}
