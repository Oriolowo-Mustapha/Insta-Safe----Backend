using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using InstaSafe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Infrastructure.Repositories;

public class VendorRepository : IVendorRepository
{
    private readonly AppDbContext _db;
    public VendorRepository(AppDbContext db) => _db = db;

    public Task<Vendor?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Vendors.FirstOrDefaultAsync(v => v.Id == id, ct)!;

    public Task<Vendor?> GetByPhoneAsync(string phone, CancellationToken ct)
        => _db.Vendors.FirstOrDefaultAsync(v => v.Phone == phone, ct)!;

    public Task<Vendor?> GetByEmailAsync(string email, CancellationToken ct)
        => _db.Vendors.FirstOrDefaultAsync(v => v.Email != null && v.Email.ToLower() == email.ToLower(), ct)!;

    public Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct)
        => _db.Vendors.AnyAsync(v => v.Phone == phone, ct);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct)
        => _db.Vendors.AnyAsync(v => v.Email != null && v.Email.ToLower() == email.ToLower(), ct);

    public async Task AddAsync(Vendor vendor, CancellationToken ct)
        => await _db.Vendors.AddAsync(vendor, ct);

    public Task<List<Vendor>> ListAsync(int page, int pageSize, bool? activeOnly, CancellationToken ct)
    {
        var q = _db.Vendors.AsNoTracking().AsQueryable();
        if (activeOnly == true) q = q.Where(v => v.IsActive);
        return q.OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
