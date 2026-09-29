using FluentValidation;
using InstaSafe.Application.Common.Helpers;

namespace InstaSafe.Application.Features.Vendors.Commands.UpdateVendorPhone;

public class UpdateVendorPhoneCommandValidator : AbstractValidator<UpdateVendorPhoneCommand>
{
    public UpdateVendorPhoneCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.Phone)
            .NotEmpty().MaximumLength(20)
            .Must(p => System.Text.RegularExpressions.Regex.IsMatch(
                PhoneNormalizer.Normalize(p), @"^234\d{10}$"))
            .WithMessage("Phone must be a valid Nigerian mobile number (e.g. 08031234567).");
    }
}
