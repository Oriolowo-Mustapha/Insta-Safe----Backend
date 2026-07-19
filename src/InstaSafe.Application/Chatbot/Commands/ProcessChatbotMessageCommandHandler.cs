using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Application.Common.Models.Monnify;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace InstaSafe.Application.Chatbot.Commands;

public class ProcessChatbotMessageCommandHandler : IRequestHandler<ProcessChatbotMessageCommand, Result<string>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChatbotAiService _aiService;
    private readonly IWhatsAppMessagingService _messagingService;
    private readonly IMonnifyPaymentService _monnifyPaymentService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ProcessChatbotMessageCommandHandler(
        IUnitOfWork unitOfWork, 
        IChatbotAiService aiService, 
        IWhatsAppMessagingService messagingService,
        IMonnifyPaymentService monnifyPaymentService,
        IDateTimeProvider dateTimeProvider)
    {
        _unitOfWork = unitOfWork;
        _aiService = aiService;
        _messagingService = messagingService;
        _monnifyPaymentService = monnifyPaymentService;
        _dateTimeProvider = dateTimeProvider;
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

        switch (session.CurrentState)
        {
            case ChatbotState.Idle:
                var aiResult = await _aiService.ParseIntentAsync(request.MessageText, cancellationToken);
                
                if (aiResult.Intent == "CreateOrder")
                {
                    if (!string.IsNullOrWhiteSpace(aiResult.ReplyMessage))
                    {
                        // The AI realized some details (Item, Price, Location) are missing and asked for them
                        replyMessage = aiResult.ReplyMessage;
                        // Stay in Idle state so they can reply with missing details in their next natural language message
                    }
                    else
                    {
                        // We have all details
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
                else if (aiResult.Intent == "CheckStatus")
                {
                    session.UpdateState(ChatbotState.AwaitingOrderStatusReference);
                    replyMessage = "Sure, I can check that for you. Please provide your Order Reference ID.";
                }
                else
                {
                    replyMessage = aiResult.ReplyMessage ?? "I'm sorry, I didn't understand. Do you want to *Create an Order* or *Check Order Status*?";
                }
                break;

            case ChatbotState.ConfirmingOrder:
                if (request.MessageText.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase))
                {
                    var finalData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(session.TemporaryData ?? "{}");
                    var amountVal = finalData["Price"].GetDecimal();
                    var itemVal = finalData["Item"].GetString() ?? "Order via Chatbot";
                    var locationVal = finalData["Location"].GetString() ?? "Unknown Location";
                    
                    var merchants = await _unitOfWork.Repository<Merchant>().GetAllAsync(cancellationToken);
                    var merchant = merchants.FirstOrDefault(); // Hackathon default
                    
                    if (merchant == null)
                    {
                        replyMessage = "System Error: No merchant account found linked to your number.";
                        session.Reset();
                        break;
                    }

                    var newOrder = new Order
                    {
                        OrderReference = "CB-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper(),
                        MerchantId = merchant.Id,
                        ItemName = itemVal,
                        Price = amountVal,
                        ItemDescription = $"Delivery to {locationVal}"
                    };
                    
                    _unitOfWork.Orders.Add(newOrder);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    // Generate Monnify Payment Link
                    var initRequest = new InitTransactionRequest(
                        Amount: amountVal,
                        CustomerName: merchant.BusinessName,
                        CustomerEmail: "buyer@example.com", // Since this is a quick chatbot flow, we use a placeholder or ask for it
                        PaymentReference: newOrder.OrderReference,
                        PaymentDescription: $"Escrow for {itemVal}",
                        CurrencyCode: "NGN",
                        ContractCode: "YOUR_CONTRACT_CODE", // Would come from config in real app
                        RedirectUrl: $"https://instasafe.com/orders/{newOrder.OrderReference}",
                        PaymentMethods: new[] { "CARD", "ACCOUNT_TRANSFER" },
                        IncomeSplitConfig: null
                    );

                    try
                    {
                        var paymentResult = await _monnifyPaymentService.InitializeTransactionAsync(initRequest, cancellationToken);
                        if (paymentResult.RequestSuccessful && paymentResult.ResponseBody != null)
                        {
                            replyMessage = $"Success! Order *{newOrder.OrderReference}* created.\n\nHere's your escrow link: {paymentResult.ResponseBody.CheckoutUrl}\nShare with your buyer.";
                        }
                        else
                        {
                            replyMessage = $"Order created (*{newOrder.OrderReference}*) but failed to generate payment link: {paymentResult.ResponseMessage}";
                        }
                    }
                    catch (Exception)
                    {
                        replyMessage = $"Order *{newOrder.OrderReference}* created, but there was an error connecting to the payment provider. We will retry link generation later.";
                    }

                    session.Reset();
                }
                else if (request.MessageText.Trim().Equals("no", StringComparison.OrdinalIgnoreCase))
                {
                    replyMessage = "Order creation cancelled. How else can I help you today?";
                    session.Reset();
                }
                else
                {
                    replyMessage = "Please reply with *Yes* or *No* to confirm the order.";
                }
                break;

            case ChatbotState.AwaitingOrderStatusReference:
                if (Guid.TryParse(request.MessageText, out Guid orderId))
                {
                    var order = await _unitOfWork.Orders.GetByIdWithAllAsync(orderId, cancellationToken);
                    if (order != null)
                    {
                        replyMessage = $"Order Status for {orderId.ToString().Substring(0,8)}:\nStatus: *{order.Status}*\nAmount: {order.Price} NGN";
                    }
                    else
                    {
                        replyMessage = "I couldn't find an order with that ID.";
                    }
                }
                else
                {
                    // Maybe it's a string reference
                    var orders = await _unitOfWork.Repository<Order>().FindAsync(o => o.OrderReference == request.MessageText.Trim(), cancellationToken);
                    var order = orders.FirstOrDefault();
                    if (order != null)
                    {
                        replyMessage = $"Order Status for {order.OrderReference}:\nStatus: *{order.Status}*\nAmount: {order.Price} NGN";
                    }
                    else
                    {
                        replyMessage = "I couldn't find an order with that ID. Please ensure it's correct.";
                    }
                }
                session.Reset();
                break;

            default:
                session.Reset();
                replyMessage = "Session reset. How can I help you today?";
                break;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Send reply to WhatsApp
        await _messagingService.SendMessageAsync(request.PhoneNumber, replyMessage, cancellationToken);

        return Result<string>.Success(replyMessage);
    }
}
