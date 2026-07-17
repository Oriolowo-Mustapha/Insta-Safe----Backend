using FluentValidation;

namespace InstaSafe.Application.Payments.Commands.ProcessMonnifyWebhook;

public class ProcessMonnifyWebhookCommandValidator : AbstractValidator<ProcessMonnifyWebhookCommand>
{
    public ProcessMonnifyWebhookCommandValidator()
    {
        RuleFor(x => x.Payload)
            .NotEmpty().WithMessage("Webhook payload is required.");

        RuleFor(x => x.Signature)
            .NotEmpty().WithMessage("Webhook signature is required for validation.");
    }
}
