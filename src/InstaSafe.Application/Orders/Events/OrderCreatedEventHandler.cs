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
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public OrderCreatedEventHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IInAppNotificationService inAppNotificationService,
        ILogger<OrderCreatedEventHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _inAppNotificationService = inAppNotificationService;
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

        // Notify Merchant
        if (order.Merchant != null)
        {
            await _inAppNotificationService.SendNotificationAsync(
                order.Merchant.UserId,
                "New Order Created",
                $"An order ({order.ItemName}) has been created by {order.Buyer?.FirstName} {order.Buyer?.LastName}.",
                cancellationToken);

            await _emailService.SendEmailAsync(
                order.Merchant.Email,
                "New InstaSafe Order Created",
                $"An order for <b>{order.ItemName}</b> has been created by {order.Buyer?.FirstName} {order.Buyer?.LastName}.",
                cancellationToken);
        }

        // Notify Buyer
        if (order.Buyer != null)
        {
            await _emailService.SendEmailAsync(
                order.Buyer.Email,
                "Your InstaSafe Order",
                $"Your order for <b>{order.ItemName}</b> has been created. Please complete the escrow payment to secure the transaction.",
                cancellationToken);
        }
    }
}
