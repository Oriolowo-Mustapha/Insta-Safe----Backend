using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Orders.Events;

public class EscrowLinkGeneratedEventHandler : INotificationHandler<EscrowLinkGeneratedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IInAppNotificationService _inAppNotificationService;
    private readonly ILogger<EscrowLinkGeneratedEventHandler> _logger;

    public EscrowLinkGeneratedEventHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IInAppNotificationService inAppNotificationService,
        ILogger<EscrowLinkGeneratedEventHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _inAppNotificationService = inAppNotificationService;
        _logger = logger;
    }

    public async Task Handle(EscrowLinkGeneratedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Escrow link generated for order {OrderId}: {Link}", 
            notification.OrderId, notification.EscrowLinkUrl);

        var order = await _context.Orders
            .Include(o => o.Buyer)
            .FirstOrDefaultAsync(o => o.Id == notification.OrderId, cancellationToken);

        if (order?.Buyer != null)
        {
            await _emailService.SendEmailAsync(
                order.Buyer.Email,
                "Your Escrow Payment Link",
                $"<p>Your escrow payment link for <b>{order.ItemName}</b> is ready.</p><p><a href=\"{notification.EscrowLinkUrl}\">Click here to pay securely</a></p>",
                cancellationToken);
        }
    }
}
