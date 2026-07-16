using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Orders.Events;

public class OrderDispatchedEventHandler : INotificationHandler<OrderDispatchedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IInAppNotificationService _inAppNotificationService;
    private readonly ILogger<OrderDispatchedEventHandler> _logger;

    public OrderDispatchedEventHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IInAppNotificationService inAppNotificationService,
        ILogger<OrderDispatchedEventHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _inAppNotificationService = inAppNotificationService;
        _logger = logger;
    }

    public async Task Handle(OrderDispatchedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Order {OrderId} dispatched. Session: {SessionId}", 
            notification.OrderId, notification.DeliverySessionId);

        var order = await _context.Orders
            .Include(o => o.Buyer)
            .FirstOrDefaultAsync(o => o.Id == notification.OrderId, cancellationToken);

        if (order?.Buyer != null)
        {
            await _emailService.SendEmailAsync(
                order.Buyer.Email,
                "Order Out For Delivery",
                $"Your order <b>{order.ItemName}</b> has been dispatched. Please make sure to present your Delivery QR Code to the dispatcher to confirm receipt.",
                cancellationToken);
        }
    }
}
