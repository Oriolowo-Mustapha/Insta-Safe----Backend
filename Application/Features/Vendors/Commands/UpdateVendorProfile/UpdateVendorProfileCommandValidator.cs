using FluentValidation;

namespace InstaSafe.Application.Features.Vendors.Commands.UpdateVendorProfile;

public class UpdateVendorProfileCommandValidator : AbstractValidator<UpdateVendorProfileCommand>
{
    public UpdateVendorProfileCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
    }
}
