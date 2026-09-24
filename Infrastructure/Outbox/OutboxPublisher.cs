using InstaSafe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using InstaSafe.Application.Common.Interfaces;

namespace InstaSafe.Infrastructure.Outbox;

public class OutboxPublisher : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(IServiceProvider sp, ILogger<OutboxPublisher> logger)
    {
        _sp = sp; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var wa = scope.ServiceProvider.GetRequiredService<IWhatsAppSender>();

                var pending = await db.OutboxMessages
                    .Where(m => m.PublishedAt == null && m.Attempts < 5)
                    .OrderBy(m => m.CreatedAt).Take(20).ToListAsync(ct);

                foreach (var msg in pending)
                {
                    try
                    {
                        _logger.LogInformation("Publishing outbox {Type} {Id}", msg.Type, msg.Id);
                        // MVP: mark published; extend per-type dispatch (email/WhatsApp/read-model) here.
                        msg.PublishedAt = DateTimeOffset.UtcNow;
                        msg.Attempts++;
                    }
                    catch (Exception ex)
                    {
                        msg.Attempts++;
                        msg.Error = ex.Message;
                    }
                }
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox poll failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(15), ct);
        }
    }
}
