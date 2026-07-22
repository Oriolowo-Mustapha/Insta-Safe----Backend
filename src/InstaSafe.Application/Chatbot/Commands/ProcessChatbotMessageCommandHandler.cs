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
using InstaSafe.Application.Orders.Commands.CreateOrder;

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
    private readonly ISender _sender;

    public ProcessChatbotMessageCommandHandler(
        IUnitOfWork unitOfWork, 
        IChatbotAiService aiService, 
        IWhatsAppMessagingService messagingService,
        IMonnifyPaymentService monnifyPaymentService,
        IDateTimeProvider dateTimeProvider,
        IConfiguration configuration,
        IFraudScoringEngine fraudScoringEngine,
        IOptions<MonnifyOptions> options,
        ISender sender)
    {
        _unitOfWork = unitOfWork;
        _aiService = aiService;
        _messagingService = messagingService;
        _monnifyPaymentService = monnifyPaymentService;
        _dateTimeProvider = dateTimeProvider;
        _configuration = configuration;
        _fraudScoringEngine = fraudScoringEngine;
        _options = options.Value;
        _sender = sender;
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
                            if (data != null && data.TryGetValue("ItemName", out var itemEl) && data.TryGetValue("Price", out var priceEl) && data.TryGetValue("BuyerFirstName", out var fnEl) && data.TryGetValue("BuyerLastName", out var lnEl) && data.TryGetValue("BuyerEmail", out var emailEl) && data.TryGetValue("BuyerPhone", out var phoneEl))
                            {
                                var item = itemEl.ValueKind == JsonValueKind.String ? itemEl.GetString() : itemEl.ToString();
                                
                                decimal price = 0;
                                if (priceEl.ValueKind == JsonValueKind.Number) price = priceEl.GetDecimal();
                                else decimal.TryParse(priceEl.GetString()?.Replace(",", "").Replace("N", ""), out price);
                                
                                var buyerFirstName = fnEl.ValueKind == JsonValueKind.String ? fnEl.GetString() : fnEl.ToString();
                                var buyerLastName = lnEl.ValueKind == JsonValueKind.String ? lnEl.GetString() : lnEl.ToString();
                                var buyerPhone = phoneEl.ValueKind == JsonValueKind.String ? phoneEl.GetString() : phoneEl.ToString();
                                var buyerEmail = emailEl.ValueKind == JsonValueKind.String ? emailEl.GetString() : emailEl.ToString();
                                
                                var desc = data.TryGetValue("Description", out var dEl) ? (dEl.ValueKind == JsonValueKind.String ? dEl.GetString() : dEl.ToString()) : "";
                                var location = data.TryGetValue("DeliveryAddress", out var lEl) ? (lEl.ValueKind == JsonValueKind.String ? lEl.GetString() : lEl.ToString()) : "";
                                var dispatcherPhone = data.TryGetValue("DispatcherPhone", out var dpEl) ? (dpEl.ValueKind == JsonValueKind.String ? dpEl.GetString() : dpEl.ToString()) : "";

                                if (string.IsNullOrWhiteSpace(buyerEmail) || !buyerEmail.Contains("@")) buyerEmail = "unknown@example.com";
                                
                                session.UpdateState(ChatbotState.ConfirmingOrder, JsonSerializer.Serialize(new {
                                    ItemName = item,
                                    Price = price,
                                    Description = desc,
                                    BuyerFirstName = buyerFirstName,
                                    BuyerLastName = buyerLastName,
                                    BuyerEmail = buyerEmail,
                                    BuyerPhone = buyerPhone,
                                    DeliveryAddress = location,
                                    DispatcherPhone = dispatcherPhone
                                }));

                                replyMessage = $"Got it! Here is your order summary:\n" +
                                               $"- Item: {item}\n" +
                                               $"- Description: {desc}\n" +
                                               $"- Price: N{price:N0}\n" +
                                               $"- Buyer: {buyerFirstName} {buyerLastName}\n" +
                                               $"- Email: {buyerEmail}\n" +
                                               $"- Phone: {buyerPhone}\n" +
                                               $"- Delivery To: {location}\n" +
                                               $"- Dispatcher: {(string.IsNullOrWhiteSpace(dispatcherPhone) ? "None yet" : dispatcherPhone)}\n\n" +
                                               $"Confirm? (Yes/No)";
                            }
                            else
                            {
                                replyMessage = "I couldn't extract all the required details (Item Name, Price, First Name, Last Name, Email, Phone). Please ensure you provide all of them.";
                            }
                        }
                        catch
                        {
                            replyMessage = "I couldn't process the order details. Please try again with the item, price, location, and buyer's phone number.";
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
                    try
                    {
                        var finalData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(session.TemporaryData ?? "{}");
                        var amountVal = finalData.TryGetValue("Price", out var pEl) ? (pEl.ValueKind == JsonValueKind.Number ? pEl.GetDecimal() : decimal.Parse(pEl.GetString() ?? "0")) : 0;
                        var itemVal = finalData.TryGetValue("ItemName", out var iEl) ? iEl.GetString() ?? "Order via Chatbot" : "Order via Chatbot";
                        var descVal = finalData.TryGetValue("Description", out var dEl) ? dEl.GetString() : null;
                        var locationVal = finalData.TryGetValue("DeliveryAddress", out var lEl) ? lEl.GetString() : null;
                        
                        var buyerFirstName = finalData.TryGetValue("BuyerFirstName", out var fnEl) ? fnEl.GetString() ?? "Unknown" : "Unknown";
                        var buyerLastName = finalData.TryGetValue("BuyerLastName", out var lnEl) ? lnEl.GetString() ?? "Buyer" : "Buyer";
                        var buyerEmailVal = finalData.TryGetValue("BuyerEmail", out var beEl) ? beEl.GetString() ?? "unknown@example.com" : "unknown@example.com";
                        var buyerPhoneVal = finalData.TryGetValue("BuyerPhone", out var bpEl) ? bpEl.GetString() ?? "" : "";
                        var dispatcherPhoneVal = finalData.TryGetValue("DispatcherPhone", out var dpEl) ? dpEl.GetString() : null;

                        var createCommand = new CreateOrderCommand(
                            MerchantId: merchant!.Id,
                            ItemName: itemVal,
                            ItemDescription: descVal,
                            ItemImageUrl: null,
                            Price: amountVal,
                            DeliveryAddress: locationVal,
                            BuyerFirstName: buyerFirstName,
                            BuyerLastName: buyerLastName,
                            BuyerEmail: buyerEmailVal,
                            BuyerPhone: buyerPhoneVal,
                            DispatcherPhone: string.IsNullOrWhiteSpace(dispatcherPhoneVal) ? null : dispatcherPhoneVal
                        );

                        var result = await _sender.Send(createCommand, cancellationToken);

                        if (result.Succeeded)
                        {
                            replyMessage = $"Success! Order *{result.Data.OrderReference}* has been created.\n\nThe escrow payment link has been sent directly to the buyer's WhatsApp and Email.";
                        }
                        else
                        {
                            replyMessage = $"Failed to create order: {result.Error}";
                        }
                    }
                    catch (Exception ex)
                    {
                        System.IO.File.WriteAllText(@"C:\Users\MUSTAPHA\source\repos\Instasafe -- Backend\error_log.txt", ex.ToString());
                        replyMessage = $"Sorry, I had trouble processing the order details. Please type 'I want to create an order' to start again. ({ex.Message})";
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
