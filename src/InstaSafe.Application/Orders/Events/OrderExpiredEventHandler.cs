using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Orders.Events;

public class OrderExpiredEventHandler : INotificationHandler<OrderExpiredEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IInAppNotificationService _inAppNotificationService;
    private readonly ILogger<OrderExpiredEventHandler> _logger;

    public OrderExpiredEventHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IInAppNotificationService inAppNotificationService,
        ILogger<OrderExpiredEventHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _inAppNotificationService = inAppNotificationService;
        _logger = logger;
    }

    public async Task Handle(OrderExpiredEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Order {OrderId} expired.", notification.OrderId);

        var order = await _context.Orders
            .Include(o => o.Merchant)
            .Include(o => o.Buyer)
            .FirstOrDefaultAsync(o => o.Id == notification.OrderId, cancellationToken);

        if (order == null) return;

        if (order.Merchant != null)
        {
            await _inAppNotificationService.SendNotificationAsync(
                order.Merchant.UserId,
                "Order Expired",
                $"Your order ({order.ItemName}) has expired due to non-payment.",
                cancellationToken);

            await _emailService.SendEmailAsync(
                order.Merchant.Email,
                "Order Expired",
                $"The order for <b>{order.ItemName}</b> was cancelled as the buyer did not complete the escrow payment in time.",
                cancellationToken);
        }

        if (order.Buyer != null)
        {
            await _emailService.SendEmailAsync(
                order.Buyer.Email,
                "Order Expired",
                $"Your order for <b>{order.ItemName}</b> has expired because the escrow payment was not completed in time.",
                cancellationToken);
        }
    }
}
