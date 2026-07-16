using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Payouts.Events;

public class EscrowReleasedEventHandler : INotificationHandler<EscrowReleasedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IInAppNotificationService _inAppNotificationService;
    private readonly ILogger<EscrowReleasedEventHandler> _logger;

    public EscrowReleasedEventHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IInAppNotificationService inAppNotificationService,
        ILogger<EscrowReleasedEventHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _inAppNotificationService = inAppNotificationService;
        _logger = logger;
    }

    public async Task Handle(EscrowReleasedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Escrow released for order {OrderId}. Amount: {Amount}", 
            notification.OrderId, notification.MerchantAmount);

        var order = await _context.Orders
            .Include(o => o.Merchant)
            .FirstOrDefaultAsync(o => o.Id == notification.OrderId, cancellationToken);

        if (order?.Merchant != null)
        {
            await _inAppNotificationService.SendNotificationAsync(
                order.Merchant.UserId,
                "Funds Released",
                $"Escrow funds ({notification.MerchantAmount:C}) for {order.ItemName} have been released to your wallet.",
                cancellationToken);

            await _emailService.SendEmailAsync(
                order.Merchant.Email,
                "Escrow Funds Released",
                $"<p>Great news! The escrow funds for your order <b>{order.ItemName}</b> have been released successfully.</p><p>Amount added to your wallet: <b>{notification.MerchantAmount:C}</b></p>",
                cancellationToken);
        }
    }
}
