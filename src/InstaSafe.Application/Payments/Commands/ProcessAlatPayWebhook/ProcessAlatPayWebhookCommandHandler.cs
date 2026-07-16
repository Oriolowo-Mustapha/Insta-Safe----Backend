using System.Text.Json;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Payments.Commands.ProcessAlatPayWebhook;

public class ProcessAlatPayWebhookCommandHandler : IRequestHandler<ProcessAlatPayWebhookCommand, Result<string>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessAlatPayWebhookCommandHandler> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ProcessAlatPayWebhookCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<ProcessAlatPayWebhookCommandHandler> logger,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<string>> Handle(ProcessAlatPayWebhookCommand request, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(request.RawPayload);
        var root = document.RootElement;

        var transactionId = root.TryGetProperty("transactionId", out var tIdProp) ? tIdProp.GetString() : null;
        var status = root.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
        var eventType = root.TryGetProperty("eventType", out var eventTypeProp) ? eventTypeProp.GetString() : null;

        var alreadyProcessed = !string.IsNullOrEmpty(transactionId)
            && await _unitOfWork.WebhookEventLogs.IsProcessedAsync(transactionId, cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation("Duplicate webhook received for transaction {TransactionId}.", transactionId);
            return Result<string>.Success("Duplicate webhook ignored.");
        }

        var webhookLog = new WebhookEventLog
        {
            Source = "AlatPay",
            EventType = eventType,
            RawPayload = request.RawPayload,
            ReceivedAt = _dateTimeProvider.UtcNow,
            AlatPayTransactionId = transactionId,
            IsProcessed = false
        };

        _unitOfWork.WebhookEventLogs.Add(webhookLog);

        if (string.IsNullOrEmpty(transactionId))
        {
            webhookLog.ErrorMessage = "No transactionId found in payload.";
            webhookLog.ProcessingResult = WebhookProcessingResult.Failed;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<string>.Failure("Invalid payload.");
        }

        var escrowTx = await _unitOfWork.EscrowTransactions.GetByAlatPayTransactionIdAsync(transactionId, cancellationToken);

        if (escrowTx == null)
        {
            webhookLog.ErrorMessage = $"No escrow transaction found for {transactionId}.";
            webhookLog.ProcessingResult = WebhookProcessingResult.Failed;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<string>.Failure("Transaction not found.");
        }

        if (status?.Equals("successful", StringComparison.OrdinalIgnoreCase) == true)
        {
            var now = _dateTimeProvider.UtcNow;
            escrowTx.MarkAsFunded(now);

            var order = escrowTx.Order;
            order.MarkAsFunded(now);

            webhookLog.IsProcessed = true;
            webhookLog.ProcessedAt = _dateTimeProvider.UtcNow;
            webhookLog.ProcessingResult = WebhookProcessingResult.Success;
        }
        else
        {
            escrowTx.MarkAsFailed();
            webhookLog.IsProcessed = true;
            webhookLog.ProcessedAt = _dateTimeProvider.UtcNow;
            webhookLog.ProcessingResult = WebhookProcessingResult.Success;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<string>.Success("Webhook processed successfully.");
    }
}
