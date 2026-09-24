using FluentValidation;

namespace InstaSafe.Application.Features.Webhooks.Commands.ProcessOpenWAWebhook;

public class ProcessOpenWAWebhookCommandValidator : AbstractValidator<ProcessOpenWAWebhookCommand>
{
    public ProcessOpenWAWebhookCommandValidator() =>
        RuleFor(x => x.RawBody).NotEmpty();
}
