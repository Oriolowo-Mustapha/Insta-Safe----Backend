using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Models.Monnify;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using InstaSafe.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InstaSafe.Application.Payments.Commands.ProcessMonnifyWebhook;

public class ProcessMonnifyWebhookCommandHandler : IRequestHandler<ProcessMonnifyWebhookCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ProcessMonnifyWebhookCommandHandler> _logger;
    private readonly MonnifyOptions _options;

    public ProcessMonnifyWebhookCommandHandler(
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        ILogger<ProcessMonnifyWebhookCommandHandler> logger,
        IOptions<MonnifyOptions> options)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<Result<bool>> Handle(ProcessMonnifyWebhookCommand request, CancellationToken cancellationToken)
    {
        // 1. Validate Signature
        if (!IsValidSignature(request.Payload, request.Signature))
        {
            _logger.LogWarning("Invalid Monnify webhook signature.");
            return Result<bool>.Failure("Invalid signature.");
        }

        // 2. Parse Payload
        JsonDocument jsonDoc;
        try
        {
            jsonDoc = JsonDocument.Parse(request.Payload);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Monnify webhook payload.");
            return Result<bool>.Failure("Invalid payload format.");
        }

        var root = jsonDoc.RootElement;
        var eventType = root.GetProperty("eventType").GetString();

        if (eventType != "SUCCESSFUL_TRANSACTION")
        {
            _logger.LogInformation($"Ignoring webhook event type: {eventType}");
            return Result<bool>.Success(true);
        }

        var eventData = root.GetProperty("eventData");
        var paymentReference = eventData.GetProperty("paymentReference").GetString();
        var paymentStatus = eventData.GetProperty("paymentStatus").GetString();

        if (string.IsNullOrEmpty(paymentReference) || paymentStatus != "PAID")
        {
            _logger.LogInformation("Webhook event is not PAID or missing payment reference.");
            return Result<bool>.Success(true);
        }

        // 3. Process Transaction
        var escrowTx = await _unitOfWork.EscrowTransactions.GetByTransactionReferenceAsync(paymentReference, cancellationToken);

        if (escrowTx == null)
        {
            _logger.LogWarning($"Escrow transaction not found for reference: {paymentReference}");
            return Result<bool>.Failure("Transaction not found.");
        }

        if (escrowTx.Status != EscrowTransactionStatus.Pending)
        {
            _logger.LogInformation($"Transaction {paymentReference} is already processed (Status: {escrowTx.Status}).");
            return Result<bool>.Success(true);
        }

        var order = await _unitOfWork.Orders.GetByIdWithMerchantAsync(escrowTx.OrderId, cancellationToken);
        if (order == null)
        {
            _logger.LogError($"Order {escrowTx.OrderId} not found for transaction {paymentReference}.");
            return Result<bool>.Failure("Order not found.");
        }

        // 4. Update State
        escrowTx.MarkAsFunded(_dateTimeProvider.UtcNow);
        order.MarkAsFunded(_dateTimeProvider.UtcNow);

        var webhookLog = new WebhookEventLog
        {
            Source = "Monnify",
            EventType = eventType,
            RawPayload = request.Payload,
            ProcessedAt = _dateTimeProvider.UtcNow,
            // IsSuccessful doesn't exist, remove it
        };
        _unitOfWork.WebhookEventLogs.Add(webhookLog);

        order.AddDomainEvent(new OrderFundedEvent(order.Id, escrowTx.MonnifyTransactionReference ?? string.Empty));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"Successfully processed payment for order {order.Id}");
        return Result<bool>.Success(true);
    }

    private bool IsValidSignature(string payload, string signatureHeader)
    {
        var keyBytes = Encoding.UTF8.GetBytes(_options.SecretKey);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        var calculatedSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        return string.Equals(calculatedSignature, signatureHeader, StringComparison.OrdinalIgnoreCase);
    }
}
