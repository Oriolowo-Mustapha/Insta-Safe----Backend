using FluentValidation;

namespace InstaSafe.Application.Features.Vendors.Commands.UpdateVendorPayout;

public class UpdateVendorPayoutCommandValidator : AbstractValidator<UpdateVendorPayoutCommand>
{
    public UpdateVendorPayoutCommandValidator()
    {
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.BankCode).NotEmpty().MaximumLength(10);
    }
}
