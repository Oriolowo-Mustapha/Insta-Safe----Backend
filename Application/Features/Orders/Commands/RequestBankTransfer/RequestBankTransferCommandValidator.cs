using FluentValidation;

namespace InstaSafe.Application.Features.Orders.Commands.RequestBankTransfer;

public class RequestBankTransferCommandValidator : AbstractValidator<RequestBankTransferCommand>
{
    public RequestBankTransferCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.PreferredBank).MaximumLength(60);
    }
}
