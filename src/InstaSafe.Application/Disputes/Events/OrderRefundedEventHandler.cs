using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Disputes.Events;

public class OrderRefundedEventHandler : INotificationHandler<OrderRefundedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IInAppNotificationService _inAppNotificationService;
    private readonly ILogger<OrderRefundedEventHandler> _logger;

    public OrderRefundedEventHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IInAppNotificationService inAppNotificationService,
        ILogger<OrderRefundedEventHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _inAppNotificationService = inAppNotificationService;
        _logger = logger;
    }

    public async Task Handle(OrderRefundedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Order {OrderId} refunded. Amount: {RefundAmount:C}.",
            notification.OrderId, notification.RefundAmount);

        var order = await _context.Orders
            .Include(o => o.Buyer)
            .FirstOrDefaultAsync(o => o.Id == notification.OrderId, cancellationToken);

        if (order?.Buyer != null)
        {
            await _emailService.SendEmailAsync(
                order.Buyer.Email,
                "Order Refund Processed",
                $"<p>Your dispute regarding <b>{order.ItemName}</b> has been concluded.</p><p>A refund of <b>{notification.RefundAmount:C}</b> has been processed back to your original payment method via Monnify.</p>",
                cancellationToken);
        }
    }
}
