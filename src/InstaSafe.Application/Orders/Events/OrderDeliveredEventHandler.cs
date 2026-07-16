using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Orders.Events;

public class OrderDeliveredEventHandler : INotificationHandler<OrderDeliveredEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IInAppNotificationService _inAppNotificationService;
    private readonly ILogger<OrderDeliveredEventHandler> _logger;

    public OrderDeliveredEventHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IInAppNotificationService inAppNotificationService,
        ILogger<OrderDeliveredEventHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _inAppNotificationService = inAppNotificationService;
        _logger = logger;
    }

    public async Task Handle(OrderDeliveredEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Order {OrderId} delivered. Session: {SessionId}", 
            notification.OrderId, notification.DeliverySessionId);

        var order = await _context.Orders
            .Include(o => o.Merchant)
            .Include(o => o.Buyer)
            .FirstOrDefaultAsync(o => o.Id == notification.OrderId, cancellationToken);

        if (order == null) return;

        if (order.Merchant != null)
        {
            await _inAppNotificationService.SendNotificationAsync(
                order.Merchant.UserId,
                "Delivery Confirmed",
                $"Your order ({order.ItemName}) was successfully delivered. Funds will be released shortly.",
                cancellationToken);

            await _emailService.SendEmailAsync(
                order.Merchant.Email,
                "Delivery Successfully Confirmed",
                $"The delivery for <b>{order.ItemName}</b> has been confirmed via QR scan. Your payout will be released automatically if no dispute is raised.",
                cancellationToken);
        }

        if (order.Buyer != null)
        {
            await _emailService.SendEmailAsync(
                order.Buyer.Email,
                "Delivery Confirmation",
                $"You have successfully confirmed the receipt of <b>{order.ItemName}</b>. Thank you for using InstaSafe!",
                cancellationToken);
        }
    }
}
