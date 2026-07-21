using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using InstaSafe.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Application.Orders.Commands.DeleteOrder;

public class DeleteOrderCommandHandler : IRequestHandler<DeleteOrderCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;
    private readonly IWhatsAppMessagingService _whatsAppMessagingService;
    private readonly ILogger<DeleteOrderCommandHandler> _logger;

    public DeleteOrderCommandHandler(
        IApplicationDbContext context,
        IWhatsAppMessagingService whatsAppMessagingService,
        ILogger<DeleteOrderCommandHandler> logger)
    {
        _context = context;
        _whatsAppMessagingService = whatsAppMessagingService;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(DeleteOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
            return Result<bool>.Failure("Order not found.");

        var merchant = await _context.Merchants.FirstOrDefaultAsync(m => m.UserId == request.MerchantId, cancellationToken);
        if (merchant == null)
        {
            merchant = await _context.Merchants.FirstOrDefaultAsync(m => m.Id == request.MerchantId, cancellationToken);
            if (merchant == null)
                return Result<bool>.Failure("Merchant not found.");
        }

        if (order.MerchantId != merchant.Id)
            return Result<bool>.Failure("You do not have permission to delete this order.");

        if (order.Status != OrderStatus.Draft && order.Status != OrderStatus.PendingPayment)
            return Result<bool>.Failure("Only un-funded orders can be deleted.");

        _context.Orders.Remove(order);

        var escrowTx = await _context.EscrowTransactions.FirstOrDefaultAsync(e => e.OrderId == order.Id, cancellationToken);
        if (escrowTx != null)
        {
            _context.EscrowTransactions.Remove(escrowTx);
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Send cancellation message to dispatcher if applicable
        if (!string.IsNullOrEmpty(order.DispatcherPhone))
        {
            try
            {
                var message = $"InstaSafe Alert 🚫\n\n" +
                              $"The pickup assignment for Order #{order.OrderReference} ({order.ItemName}) has been cancelled by the merchant.";
                await _whatsAppMessagingService.SendMessageAsync(order.DispatcherPhone, message, cancellationToken);
                _logger.LogInformation("Sent cancellation WhatsApp message to dispatcher for order {OrderId}", order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send cancellation WhatsApp message to dispatcher for order {OrderId}", order.Id);
            }
        }

        return Result<bool>.Success(true);
    }
}
