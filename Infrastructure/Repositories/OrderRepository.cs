using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using InstaSafe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;
    public OrderRepository(Persistence.AppDbContext db) => _db = db;

    public async Task AddAsync(Order order, CancellationToken ct)
        => await _db.Orders.AddAsync(order, ct);

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
        => _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct)!;

    public Task<Order?> GetByPaystackRefAsync(string reference, CancellationToken ct)
        => _db.Orders.FirstOrDefaultAsync(o => o.PaystackReference == reference, ct)!;

    public Task<List<Order>> ListAsync(int page, int pageSize, CancellationToken ct)
        => _db.Orders.AsNoTracking().OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

    public Task<List<Order>> ListByVendorAsync(Guid vendorId, string vendorPhone, int page, int pageSize, CancellationToken ct)
        => _db.Orders.AsNoTracking()
            .Where(o => o.VendorId == vendorId || o.VendorPhone == vendorPhone)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

    public Task<List<Order>> ListByDriverAsync(Guid driverId, string driverPhone, int page, int pageSize, CancellationToken ct)
        => _db.Orders.AsNoTracking()
            .Where(o => o.DriverId == driverId || (o.DriverPhone != null && o.DriverPhone == driverPhone))
            .Where(o => o.Status == Domain.Enums.OrderStatus.Held || o.Status == Domain.Enums.OrderStatus.Delivered)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

    public Task SaveAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
