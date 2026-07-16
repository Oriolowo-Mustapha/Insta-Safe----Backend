using FluentValidation;

namespace InstaSafe.Application.Disputes.Commands.ResolveDispute;

public class ResolveDisputeCommandValidator : AbstractValidator<ResolveDisputeCommand>
{
    public ResolveDisputeCommandValidator()
    {
        RuleFor(v => v.DisputeId).NotEmpty();
        RuleFor(v => v.Resolution)
            .NotEmpty()
            .Must(r => r.Equals("refund", StringComparison.OrdinalIgnoreCase) || r.Equals("release", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Resolution must be either 'refund' or 'release'.");
        RuleFor(v => v.ResolvedByUserId).NotEmpty();
    }
}
