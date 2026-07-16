using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Payments.Commands.ProcessAlatPayWebhook;

public record ProcessAlatPayWebhookCommand(string RawPayload, string? Signature) : IRequest<Result<string>>;
