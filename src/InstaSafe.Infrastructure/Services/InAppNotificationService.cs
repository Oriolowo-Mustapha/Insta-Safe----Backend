using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Infrastructure.Services;

public class InAppNotificationService : IInAppNotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<InAppNotificationService> _logger;

    public InAppNotificationService(IApplicationDbContext context, ILogger<InAppNotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SendNotificationAsync(Guid userId, string title, string message, CancellationToken ct = default)
    {
        var notification = new InAppNotification(userId, title, message);
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("In-app notification created for User {UserId}: {Title}", userId, title);
    }
}
