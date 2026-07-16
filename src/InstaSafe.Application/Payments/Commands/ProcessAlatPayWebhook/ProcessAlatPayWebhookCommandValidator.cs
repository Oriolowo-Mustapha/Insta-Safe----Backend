using FluentValidation;

namespace InstaSafe.Application.Payments.Commands.ProcessAlatPayWebhook;

public class ProcessAlatPayWebhookCommandValidator : AbstractValidator<ProcessAlatPayWebhookCommand>
{
    public ProcessAlatPayWebhookCommandValidator()
    {
        RuleFor(v => v.RawPayload)
            .NotEmpty().WithMessage("Webhook payload is required.");
    }
}
