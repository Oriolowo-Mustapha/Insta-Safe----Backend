using FluentValidation;

namespace InstaSafe.Application.Features.Orders.Commands.ParseOrderText;

public class ParseOrderTextCommandValidator : AbstractValidator<ParseOrderTextCommand>
{
    public ParseOrderTextCommandValidator()
    {
        RuleFor(x => x.VendorPhone).NotEmpty();
        RuleFor(x => x.RawText).NotEmpty().MaximumLength(2000);
    }
}
