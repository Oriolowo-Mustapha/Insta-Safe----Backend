using FluentValidation;

namespace InstaSafe.Application.Disputes.Commands.RaiseDispute;

public class RaiseDisputeCommandValidator : AbstractValidator<RaiseDisputeCommand>
{
    public RaiseDisputeCommandValidator()
    {
        RuleFor(v => v.OrderId).NotEmpty();
        RuleFor(v => v.BuyerId).NotEmpty();
        RuleFor(v => v.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(v => v.EvidenceUrls).MaximumLength(4000).When(v => !string.IsNullOrEmpty(v.EvidenceUrls));
    }
}
