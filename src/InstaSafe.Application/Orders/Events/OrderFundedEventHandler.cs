using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Orders.Events;

public class OrderFundedEventHandler : INotificationHandler<OrderFundedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IInAppNotificationService _inAppNotificationService;
    private readonly ILogger<OrderFundedEventHandler> _logger;

    public OrderFundedEventHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IInAppNotificationService inAppNotificationService,
        ILogger<OrderFundedEventHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _inAppNotificationService = inAppNotificationService;
        _logger = logger;
    }

    public async Task Handle(OrderFundedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Order {OrderId} funded. TxId: {TxId}", 
            notification.OrderId, notification.MonnifyTransactionReference);

        var order = await _context.Orders
            .Include(o => o.Merchant)
            .Include(o => o.Buyer)
            .FirstOrDefaultAsync(o => o.Id == notification.OrderId, cancellationToken);

        if (order == null) return;

        if (order.Merchant != null)
        {
            await _inAppNotificationService.SendNotificationAsync(
                order.Merchant.UserId,
                "Funds Secured in Escrow",
                $"Funds for {order.ItemName} are now secured in Escrow. You may dispatch the item.",
                cancellationToken);

            await _emailService.SendEmailAsync(
                order.Merchant.Email,
                "Funds Secured in Escrow - Safe to Dispatch",
                $"Good news! The funds for your order <b>{order.ItemName}</b> have been fully secured in Escrow. You can now safely dispatch the item to {order.Buyer?.FirstName} {order.Buyer?.LastName}.",
                cancellationToken);
        }

        if (order.Buyer != null)
        {
            await _emailService.SendEmailAsync(
                order.Buyer.Email,
                "Payment Receipt",
                $"<p>Your payment for <b>{order.ItemName}</b> was successful.</p><p>Transaction ID: {notification.MonnifyTransactionReference}</p><p>The funds are now safely held in Escrow until you confirm delivery.</p>",
                cancellationToken);
        }
    }
}
