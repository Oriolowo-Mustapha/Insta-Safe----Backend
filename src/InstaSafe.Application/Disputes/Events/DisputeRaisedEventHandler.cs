using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Disputes.Events;

public class DisputeRaisedEventHandler : INotificationHandler<DisputeRaisedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IInAppNotificationService _inAppNotificationService;
    private readonly ILogger<DisputeRaisedEventHandler> _logger;

    public DisputeRaisedEventHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IInAppNotificationService inAppNotificationService,
        ILogger<DisputeRaisedEventHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _inAppNotificationService = inAppNotificationService;
        _logger = logger;
    }

    public async Task Handle(DisputeRaisedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dispute {DisputeId} raised for order {OrderId}", 
            notification.DisputeId, notification.OrderId);

        var order = await _context.Orders
            .Include(o => o.Merchant)
            .Include(o => o.Buyer)
            .FirstOrDefaultAsync(o => o.Id == notification.OrderId, cancellationToken);

        if (order == null) return;

        if (order.Merchant != null)
        {
            await _inAppNotificationService.SendNotificationAsync(
                order.Merchant.UserId,
                "Dispute Raised",
                $"A dispute has been raised by the buyer for {order.ItemName}. Escrow release is halted.",
                cancellationToken);

            await _emailService.SendEmailAsync(
                order.Merchant.Email,
                "Dispute Raised",
                $"A dispute has been raised regarding your order <b>{order.ItemName}</b>. Funds are frozen while the dispute is reviewed. Please check the dashboard to provide evidence if necessary.",
                cancellationToken);
        }
        
        if (order.Buyer != null)
        {
            await _emailService.SendEmailAsync(
                order.Buyer.Email,
                "Dispute Recorded",
                $"Your dispute for <b>{order.ItemName}</b> has been received. Our admin team will review it shortly. The funds are currently frozen in escrow.",
                cancellationToken);
        }
    }
}
