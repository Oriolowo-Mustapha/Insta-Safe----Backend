using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Orders.Events;

public class OrderCreatedEventHandler : INotificationHandler<OrderCreatedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IInAppNotificationService _inAppNotificationService;
    private readonly IWhatsAppMessagingService _whatsappService;
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public OrderCreatedEventHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IInAppNotificationService inAppNotificationService,
        IWhatsAppMessagingService whatsappService,
        ILogger<OrderCreatedEventHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _inAppNotificationService = inAppNotificationService;
        _whatsappService = whatsappService;
        _logger = logger;
    }

    public async Task Handle(OrderCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Order {OrderId} created.", notification.OrderId);

        var order = await _context.Orders
            .Include(o => o.Merchant)
            .Include(o => o.Buyer)
            .FirstOrDefaultAsync(o => o.Id == notification.OrderId, cancellationToken);

        if (order == null) return;

        // Send Escrow Link to Buyer (WhatsApp & Email)
        bool sentToBuyerWa = false;
        if (order.Buyer != null)
        {
            var buyerEmailMsg = $"Your order for <b>{order.ItemName}</b> has been created.<br/><br/>Please complete the escrow payment to secure the transaction using this link: <a href='{order.EscrowLinkUrl}'>{order.EscrowLinkUrl}</a>";
            await _emailService.SendEmailAsync(order.Buyer.Email, "Your InstaSafe Order", buyerEmailMsg, cancellationToken);

            if (!string.IsNullOrEmpty(order.Buyer.Phone))
            {
                try
                {
                    string buyerWaMsg = $"InstaSafe Alert ✅\n\n" +
                                        $"Your order for {order.ItemName} has been created!\n\n" +
                                        $"Please complete the escrow payment using this link to secure the transaction:\n" +
                                        $"{order.EscrowLinkUrl}\n\n" +
                                        $"(Please do not reply to this automated message.)";
                    await _whatsappService.SendMessageAsync(order.Buyer.Phone, buyerWaMsg, cancellationToken);
                    sentToBuyerWa = true;
                }
                catch
                {
                    sentToBuyerWa = false;
                }
            }
        }

        // Notify Merchant
        if (order.Merchant != null)
        {
            await _inAppNotificationService.SendNotificationAsync(
                order.Merchant.UserId,
                "New Order Created",
                $"An order ({order.ItemName}) has been created for {order.Buyer?.FirstName} {order.Buyer?.LastName}.",
                cancellationToken);

            var merchantEmailMsg = $"An order for <b>{order.ItemName}</b> has been created.<br/><br/>";
            merchantEmailMsg += sentToBuyerWa 
                ? "The payment link has been successfully sent to the buyer's email and WhatsApp." 
                : "The payment link was sent to the buyer's email, but we couldn't deliver it to their WhatsApp.";

            await _emailService.SendEmailAsync(order.Merchant.Email, "New InstaSafe Order Created", merchantEmailMsg, cancellationToken);
                
            if (!string.IsNullOrEmpty(order.Merchant.Phone))
            {
                string merchantWaMsg = $"InstaSafe Alert ✅\n\n" +
                                       $"Order #{order.OrderReference} for {order.ItemName} has been created.\n\n";
                
                merchantWaMsg += sentToBuyerWa 
                    ? "The payment link has been successfully sent to the buyer's email and WhatsApp." 
                    : "The payment link was sent to the buyer's email, but we failed to deliver it to their WhatsApp.";
                
                merchantWaMsg += "\n\n(Please do not reply to this automated message.)";

                await _whatsappService.SendMessageAsync(order.Merchant.Phone, merchantWaMsg, cancellationToken);
            }
        }
    }
}
