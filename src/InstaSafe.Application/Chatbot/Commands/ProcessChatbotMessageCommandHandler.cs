using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Models.Monnify;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.Options;

namespace InstaSafe.Application.Chatbot.Commands;

public class ProcessChatbotMessageCommandHandler : IRequestHandler<ProcessChatbotMessageCommand, Result<string>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChatbotAiService _aiService;
    private readonly IWhatsAppMessagingService _messagingService;
    private readonly IMonnifyPaymentService _monnifyPaymentService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConfiguration _configuration;
    private readonly IFraudScoringEngine _fraudScoringEngine;
    private readonly MonnifyOptions _options;

    public ProcessChatbotMessageCommandHandler(
        IUnitOfWork unitOfWork, 
        IChatbotAiService aiService, 
        IWhatsAppMessagingService messagingService,
        IMonnifyPaymentService monnifyPaymentService,
        IDateTimeProvider dateTimeProvider,
        IConfiguration configuration,
        IFraudScoringEngine fraudScoringEngine,
        IOptions<MonnifyOptions> options)
    {
        _unitOfWork = unitOfWork;
        _aiService = aiService;
        _messagingService = messagingService;
        _monnifyPaymentService = monnifyPaymentService;
        _dateTimeProvider = dateTimeProvider;
        _configuration = configuration;
        _fraudScoringEngine = fraudScoringEngine;
        _options = options.Value;
    }

    public async Task<Result<string>> Handle(ProcessChatbotMessageCommand request, CancellationToken cancellationToken)
    {
        var sessionList = await _unitOfWork.Repository<ChatbotSession>()
            .FindAsync(s => s.PhoneNumber == request.PhoneNumber, cancellationToken);
        var session = sessionList.FirstOrDefault();

        if (session == null)
        {
            session = new ChatbotSession(request.PhoneNumber);
            _unitOfWork.Repository<ChatbotSession>().Add(session);
        }

        // Reset if inactive for over 15 minutes
        if ((_dateTimeProvider.UtcNow - session.LastInteractionAt).TotalMinutes > 15)
        {
            session.Reset();
        }

        string replyMessage = "";

        // Determine User Role
        var normalizedPhone = request.PhoneNumber.TrimStart('+').TrimStart('0');

        var merchantList = await _unitOfWork.Repository<Merchant>()
            .FindAsync(m => m.Phone.TrimStart('+').TrimStart('0').EndsWith(normalizedPhone) 
                         || normalizedPhone.EndsWith(m.Phone.TrimStart('+').TrimStart('0')), 
                       cancellationToken);
        var merchant = merchantList.FirstOrDefault();

        var buyerOrders = await _unitOfWork.Repository<Order>()
            .FindAsync(o => o.Buyer != null && (o.Buyer.Phone.TrimStart('+').TrimStart('0').EndsWith(normalizedPhone)
                         || normalizedPhone.EndsWith(o.Buyer.Phone.TrimStart('+').TrimStart('0'))),
                       cancellationToken);
        var latestBuyerOrder = buyerOrders.OrderByDescending(o => o.CreatedAt).FirstOrDefault();

        bool isMerchant = merchant != null;
        bool isBuyer = latestBuyerOrder != null;

        if (!isMerchant && !isBuyer)
        {
            // Hackathon fallback
            var allMerchants = await _unitOfWork.Repository<Merchant>().GetAllAsync(cancellationToken);
            merchant = allMerchants.FirstOrDefault();
            if (merchant != null)
            {
                isMerchant = true; // Default unknown to merchant for demo purposes
            }
            else
            {
                replyMessage = "Sorry, this phone number is not recognized as an active Merchant or Buyer in InstaSafe.";
                await _messagingService.SendMessageAsync(request.PhoneNumber, replyMessage, cancellationToken);
                return Result<string>.Success(replyMessage);
            }
        }

        switch (session.CurrentState)
        {
            case ChatbotState.Idle:
                // Direct keyword check for disputes
                if (request.MessageText.Contains("dispute", StringComparison.OrdinalIgnoreCase))
                {
                    if (!isBuyer)
                    {
                        replyMessage = "As a Buyer, you can only raise disputes here. However, I couldn't find any recent orders linked to your phone number.";
                        break;
                    }
                    
                    session.UpdateState(ChatbotState.RaisingDispute, null);
                    replyMessage = "To raise a dispute, you MUST attach an image of the wrong or damaged item as evidence. Please upload the image now to proceed.";
                    break;
                }

                var aiResult = await _aiService.ParseIntentAsync(request.MessageText, cancellationToken);
                
                if (aiResult.Intent == "CreateOrder")
                {
                    if (!isMerchant)
                    {
                        replyMessage = "As a Buyer, you are only allowed to raise disputes here. You cannot create orders.";
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(aiResult.ReplyMessage))
                    {
                        replyMessage = aiResult.ReplyMessage;
                    }
                    else
                    {
                        try
                        {
                            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(aiResult.ExtractedData ?? "{}");
                            if (data != null && data.TryGetValue("Item", out var itemEl) && data.TryGetValue("Price", out var priceEl) && data.TryGetValue("Location", out var locationEl))
                            {
                                var item = itemEl.GetString();
                                var price = priceEl.GetDecimal();
                                var location = locationEl.GetString();

                                session.UpdateState(ChatbotState.ConfirmingOrder, aiResult.ExtractedData);
                                replyMessage = $"Got it! {item}, N{price:N0}, delivery to {location}. Confirm? (Yes/No)";
                            }
                            else
                            {
                                replyMessage = "I couldn't extract all the details. Could you please specify the item, price, and delivery location clearly?";
                            }
                        }
                        catch
                        {
                            replyMessage = "I couldn't process the order details. Please try again with the item, price, and location.";
                        }
                    }
                }
                else
                {
                    if (isMerchant)
                        replyMessage = "As a Merchant, you can only create orders here. If you need support, please use the web dashboard.";
                    else if (isBuyer)
                        replyMessage = "As a Buyer, you can only raise disputes here. Please reply with 'I want to raise a dispute' if you have an issue with an order.";
                    else
                        replyMessage = "I'm sorry, I didn't understand. If you are a Merchant, you can create an order. If you are a Buyer, you can raise a dispute.";
                }
                break;

            case ChatbotState.ConfirmingOrder:
                if (!isMerchant)
                {
                    replyMessage = "Action not allowed.";
                    session.Reset();
                    break;
                }

                if (request.MessageText.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase))
                {
                    var finalData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(session.TemporaryData ?? "{}");
                    var amountVal = finalData["Price"].GetDecimal();
                    var itemVal = finalData["Item"].GetString() ?? "Order via Chatbot";
                    var locationVal = finalData["Location"].GetString() ?? "Unknown Location";
                    
                    var riskResult = await _fraudScoringEngine.EvaluateMerchantRiskAsync(merchant, cancellationToken);
                    
                    if (riskResult.RiskLevel == "High")
                    {
                        replyMessage = $"Transaction blocked due to high fraud risk. Reason: {string.Join(", ", riskResult.Factors)}";
                        session.Reset();
                        break;
                    }

                    var today = _dateTimeProvider.UtcNow.ToString("yyyyMMdd");
                    var prefix = $"INSTA-ORD-{today}";
                    var count = await _unitOfWork.Orders.CountOrdersByReferencePrefixAsync(prefix, cancellationToken);
                    var randomSuffix = Guid.NewGuid().ToString("N").Substring(0, 5).ToUpper();
                    var orderReference = $"{prefix}-{(count + 1):D4}-{randomSuffix}";

                    var newOrder = new Order
                    {
                        Id = Guid.NewGuid(),
                        OrderReference = orderReference,
                        MerchantId = merchant!.Id,
                        ItemName = itemVal,
                        Price = amountVal,
                        ItemDescription = $"Delivery to {locationVal}",
                        RiskScore = riskResult.Score,
                        RiskLevel = riskResult.RiskLevel
                    };

                    var frontendUrl = _configuration["FrontendUrl:Production"] ?? "https://instasafe.vercel.app";
                    var initRequest = new InitTransactionRequest(
                        Amount: amountVal,
                        CustomerName: merchant.BusinessName,
                        CustomerEmail: merchant.Email, // using merchant email since buyer is unknown via chatbot
                        PaymentReference: newOrder.OrderReference,
                        PaymentDescription: $"InstaSafe Escrow: {itemVal}",
                        CurrencyCode: "NGN",
                        ContractCode: _options.ContractCode, 
                        RedirectUrl: $"{frontendUrl}/order/{newOrder.OrderReference}",
                        PaymentMethods: new[] { "CARD", "ACCOUNT_TRANSFER", "USSD" },
                        IncomeSplitConfig: null
                    );

                    try
                    {
                        var paymentResult = await _monnifyPaymentService.InitializeTransactionAsync(initRequest, cancellationToken);
                        if (paymentResult.RequestSuccessful && paymentResult.ResponseBody != null && !string.IsNullOrEmpty(paymentResult.ResponseBody.CheckoutUrl))
                        {
                            var escrowTransaction = new EscrowTransaction
                            {
                                OrderId = newOrder.Id,
                                MonnifyTransactionReference = paymentResult.ResponseBody.TransactionReference,
                                CheckoutUrl = paymentResult.ResponseBody.CheckoutUrl,
                                TransactionReference = newOrder.OrderReference,
                                Channel = InstaSafe.Domain.Enums.PaymentChannel.Card,
                                Amount = newOrder.Price
                            };

                            newOrder.GenerateEscrowLink(paymentResult.ResponseBody.CheckoutUrl, _dateTimeProvider.UtcNow.AddMinutes(60));
                            newOrder.AddDomainEvent(new InstaSafe.Domain.Events.OrderCreatedEvent(newOrder.Id));

                            _unitOfWork.Orders.Add(newOrder);
                            _unitOfWork.EscrowTransactions.Add(escrowTransaction);
                            await _unitOfWork.SaveChangesAsync(cancellationToken);

                            replyMessage = $"Success! Order *{newOrder.OrderReference}* created.\n\nHere's your escrow link: {paymentResult.ResponseBody.CheckoutUrl}\nShare with your buyer.";
                        }
                        else
                        {
                            replyMessage = $"Order created but failed to generate payment link: {paymentResult.ResponseMessage}";
                        }
                    }
                    catch (Exception)
                    {
                        replyMessage = $"There was an error connecting to the payment provider. We could not create the order. Please try again later.";
                    }

                    session.Reset();
                }
                else if (request.MessageText.Trim().Equals("no", StringComparison.OrdinalIgnoreCase))
                {
                    replyMessage = "Order creation cancelled.";
                    session.Reset();
                }
                else
                {
                    replyMessage = "Please reply with *Yes* or *No* to confirm the order.";
                }
                break;

            case ChatbotState.RaisingDispute:
                if (!isBuyer || latestBuyerOrder == null)
                {
                    replyMessage = "Action not allowed.";
                    session.Reset();
                    break;
                }

                if (request.MessageType == "image")
                {
                    // Call the AI Vision service to analyze the image against the order description
                    var evidenceUrl = request.MessageText; // In a real OpenWA implementation, you would extract the actual media URL here
                    var visionResult = await _aiService.AnalyzeDisputeAsync(
                        latestBuyerOrder.ItemDescription ?? latestBuyerOrder.ItemName, 
                        "Dispute raised via WhatsApp with image.", 
                        evidenceUrl, 
                        cancellationToken);

                    var newDispute = new Dispute
                    {
                        Id = Guid.NewGuid(),
                        OrderId = latestBuyerOrder.Id,
                        RaisedByBuyerId = latestBuyerOrder.BuyerId!.Value,
                        Reason = "Dispute raised via WhatsApp Chatbot.",
                        EvidenceUrls = evidenceUrl
                    };
                    
                    newDispute.AugmentWithAi(visionResult.ConfidenceScore, visionResult.Summary);
                    
                    _unitOfWork.Repository<Dispute>().Add(newDispute);
                    
                    latestBuyerOrder.MarkAsDisputed(newDispute.Id);
                    
                    replyMessage = $"Dispute successfully logged for Order {latestBuyerOrder.OrderReference}!\n\n🤖 AI Verdict Preview: {visionResult.Summary}\n\nAn admin will review it shortly and the funds have been frozen.";
                    session.Reset();
                }
                else
                {
                    replyMessage = "To proceed with the dispute, you MUST upload an IMAGE of the wrong or damaged item as evidence. Please upload an image now, or reply 'Cancel' to abort.";
                    
                    if (request.MessageText.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                    {
                        replyMessage = "Dispute cancelled.";
                        session.Reset();
                    }
                }
                break;

            default:
                session.Reset();
                replyMessage = "Session reset.";
                break;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _messagingService.SendMessageAsync(request.PhoneNumber, replyMessage, cancellationToken);

        return Result<string>.Success(replyMessage);
    }
}
