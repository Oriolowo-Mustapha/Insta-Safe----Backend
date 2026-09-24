using FluentValidation;

namespace InstaSafe.Application.Features.Vendors.Commands.DeactivateVendor;

public class DeactivateVendorCommandValidator : AbstractValidator<DeactivateVendorCommand>
{
    public DeactivateVendorCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
    }
}
