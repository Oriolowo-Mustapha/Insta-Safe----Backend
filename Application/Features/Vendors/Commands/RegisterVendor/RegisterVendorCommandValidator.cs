using FluentValidation;
using InstaSafe.Application.Common.Helpers;

namespace InstaSafe.Application.Features.Vendors.Commands.RegisterVendor;

public class RegisterVendorCommandValidator : AbstractValidator<RegisterVendorCommand>
{
    public RegisterVendorCommandValidator()
    {
        RuleFor(x => x.Phone)
            .NotEmpty().MaximumLength(20)
            .Must(p => System.Text.RegularExpressions.Regex.IsMatch(
                PhoneNormalizer.Normalize(p), @"^234\d{10}$"))
            .WithMessage("Phone must be a valid Nigerian mobile number (e.g. 08031234567).");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(200).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
    }
}
