using InstaSafe.Application.Common.Models;
using MediatR;

namespace InstaSafe.Application.Payments.Commands.ProcessMonnifyWebhook;

public record ProcessMonnifyWebhookCommand(
    string Payload,
    string Signature) : IRequest<Result<bool>>;
