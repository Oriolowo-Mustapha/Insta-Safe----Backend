using FluentValidation;

namespace InstaSafe.Application.Features.Vendors.Commands.RegisterVendor;

public class RegisterVendorCommandValidator : AbstractValidator<RegisterVendorCommand>
{
    public RegisterVendorCommandValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(200).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
        RuleFor(x => x.AccountNumber).MaximumLength(20);
        RuleFor(x => x.BankCode).MaximumLength(10);
        When(x => x.AccountNumber is not null || x.BankCode is not null, () =>
        {
            RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(20);
            RuleFor(x => x.BankCode).NotEmpty().MaximumLength(10);
        });
    }
}
