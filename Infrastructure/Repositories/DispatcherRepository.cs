using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using InstaSafe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Infrastructure.Repositories;

public class DispatcherRepository : IDispatcherRepository
{
    private readonly AppDbContext _db;
    public DispatcherRepository(AppDbContext db) => _db = db;

    public Task<Dispatcher?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Dispatchers.FirstOrDefaultAsync(d => d.Id == id, ct)!;

    public Task<Dispatcher?> GetByPhoneAsync(string phone, CancellationToken ct)
        => _db.Dispatchers.FirstOrDefaultAsync(d => d.Phone == phone, ct)!;

    public Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct)
        => _db.Dispatchers.AnyAsync(d => d.Phone == phone, ct);

    public async Task AddAsync(Dispatcher dispatcher, CancellationToken ct)
        => await _db.Dispatchers.AddAsync(dispatcher, ct);

    public Task<List<Dispatcher>> ListAsync(int page, int pageSize, bool? activeOnly, CancellationToken ct)
    {
        var q = _db.Dispatchers.AsNoTracking().AsQueryable();
        if (activeOnly == true) q = q.Where(d => d.IsActive);
        return q.OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
