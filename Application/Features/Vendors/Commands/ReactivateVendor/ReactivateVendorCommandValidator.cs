using FluentValidation;

namespace InstaSafe.Application.Features.Vendors.Commands.ReactivateVendor;

public class ReactivateVendorCommandValidator : AbstractValidator<ReactivateVendorCommand>
{
    public ReactivateVendorCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
    }
}
