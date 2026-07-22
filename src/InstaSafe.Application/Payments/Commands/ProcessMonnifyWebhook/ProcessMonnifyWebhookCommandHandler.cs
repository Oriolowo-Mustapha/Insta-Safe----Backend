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
using InstaSafe.Application.Delivery.Commands.GenerateDeliveryQrCodes;

namespace InstaSafe.Application.Payments.Commands.ProcessMonnifyWebhook;

public class ProcessMonnifyWebhookCommandHandler : IRequestHandler<ProcessMonnifyWebhookCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<ProcessMonnifyWebhookCommandHandler> _logger;
    private readonly MonnifyOptions _options;
    private readonly IMediator _mediator;
    private readonly IWhatsAppMessagingService _whatsappService;
    private readonly IEmailService _emailService;

    public ProcessMonnifyWebhookCommandHandler(
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        ILogger<ProcessMonnifyWebhookCommandHandler> logger,
        IOptions<MonnifyOptions> options,
        IMediator mediator,
        IWhatsAppMessagingService whatsappService,
        IEmailService emailService)
    {
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _options = options.Value;
        _mediator = mediator;
        _whatsappService = whatsappService;
        _emailService = emailService;
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

        var order = await _unitOfWork.Orders.GetByIdWithAllAsync(escrowTx.OrderId, cancellationToken);
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
        };
        _unitOfWork.WebhookEventLogs.Add(webhookLog);

        order.AddDomainEvent(new OrderFundedEvent(order.Id, escrowTx.MonnifyTransactionReference ?? string.Empty));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation($"Successfully processed payment for order {order.Id}");

        // Automated Handoff Flow - Notify Both Parties with QR Codes
        var qrResult = await _mediator.Send(new GenerateDeliveryQrCodesCommand(order.Id), cancellationToken);
        if (qrResult.Succeeded && qrResult.Data != null)
        {
            var frontendUrl = Environment.GetEnvironmentVariable("FrontendUrl__Production") ?? "https://instasafe.vercel.app";
            var merchantScanUrl = $"{frontendUrl}/scan/pickup?orderId={order.Id}&token={qrResult.Data.MerchantQrToken}";
            var buyerScanUrl = $"{frontendUrl}/scan/deliver?orderId={order.Id}&token={qrResult.Data.BuyerQrToken}";

            var merchantQrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={Uri.EscapeDataString(merchantScanUrl)}";
            var buyerQrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={Uri.EscapeDataString(buyerScanUrl)}";
            
            var noReplyNote = "\n\n(Please do not reply to this automated message.)";

            // 1. Notify Merchant
            if (order.Merchant != null)
            {
                var merchantEmailMsg = $"The escrow payment for your order <b>{order.ItemName}</b> has been successfully funded.<br/><br/>" + 
                                       $"This is your pickup QR code. Please have the dispatcher scan this with their phone camera once they arrive to pick up the item:<br/><br/>" +
                                       $"<img src='{merchantQrUrl}' alt='Merchant QR Code' style='max-width: 200px;'/><br/><br/>" +
                                       $"Or manually share this link: <a href='{merchantScanUrl}'>Click Here to Scan</a>";
                                       
                await _emailService.SendEmailAsync(order.Merchant.Email, "Escrow Funded - Action Required", merchantEmailMsg, cancellationToken);

                if (!string.IsNullOrEmpty(order.Merchant.Phone))
                {
                    string merchantWaMsg = $"InstaSafe Alert ✅\n\n" +
                                           $"The escrow payment for your order #{order.OrderReference} has been successfully funded!\n\n" +
                                           $"Please have the dispatcher scan this QR code with their phone camera once they arrive to pick up the item.\n\n" +
                                           $"Or manually share this link:\n{merchantScanUrl}\n" +
                                           noReplyNote;
                    await _whatsappService.SendImageAsync(order.Merchant.Phone, merchantQrUrl, merchantWaMsg, cancellationToken);
                }
            }

            // 2. Notify Buyer
            if (order.Buyer != null)
            {
                var buyerEmailMsg = $"Your payment was successful and the funds are now securely held in escrow.<br/><br/>" +
                                    $"This is your delivery QR code. Please have the dispatcher scan this with their phone camera to confirm you have received the item:<br/><br/>" +
                                    $"<img src='{buyerQrUrl}' alt='Buyer QR Code' style='max-width: 200px;'/><br/><br/>" +
                                    $"Or manually share this link: <a href='{buyerScanUrl}'>Click Here to Scan</a>";
                                    
                await _emailService.SendEmailAsync(order.Buyer.Email, "Payment Successful", buyerEmailMsg, cancellationToken);

                if (!string.IsNullOrEmpty(order.Buyer.Phone))
                {
                    string buyerWaMsg = $"InstaSafe Alert ✅\n\n" +
                                        $"Your payment for Order #{order.OrderReference} was successful!\n\n" +
                                        $"Please have the dispatcher scan this QR code with their phone camera to confirm you have received the item.\n\n" +
                                        $"Or manually share this link:\n{buyerScanUrl}\n" +
                                        noReplyNote;
                    await _whatsappService.SendImageAsync(order.Buyer.Phone, buyerQrUrl, buyerWaMsg, cancellationToken);
                }
            }
        }

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
