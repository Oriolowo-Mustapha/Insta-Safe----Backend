using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Infrastructure.Persistence.Repositories;

public class DeliverySessionRepository : IDeliverySessionRepository
{
    private readonly IApplicationDbContext _context;

    public DeliverySessionRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DeliverySession?> GetBySessionIdAsync(Guid sessionId, CancellationToken ct)
    {
        return await _context.DeliverySessions
            .AsNoTracking()
            .FirstOrDefaultAsync(ds => ds.SessionId == sessionId, ct);
    }

    public void Add(DeliverySession session)
    {
        _context.DeliverySessions.Add(session);
    }
}
