using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Infrastructure.Persistence.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly IApplicationDbContext _context;

    public OrderRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> MerchantExistsAsync(Guid merchantId, CancellationToken ct)
    {
        return await _context.Merchants.AnyAsync(m => m.Id == merchantId, ct);
    }

    public async Task<int> CountOrdersByReferencePrefixAsync(string prefix, CancellationToken ct)
    {
        return await _context.Orders.CountAsync(o => o.OrderReference.StartsWith(prefix), ct);
    }

    public async Task<Order?> GetByIdWithMerchantAsync(Guid id, CancellationToken ct)
    {
        return await _context.Orders
            .Include(o => o.Merchant)
            .Include(o => o.Buyer)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<Order?> GetByIdWithEscrowAsync(Guid id, CancellationToken ct)
    {
        return await _context.Orders
            .Include(o => o.EscrowTransaction)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<Order?> GetByIdWithAllAsync(Guid id, CancellationToken ct)
    {
        return await _context.Orders
            .Include(o => o.Merchant)
            .Include(o => o.Buyer)
            .Include(o => o.EscrowTransaction)
            .Include(o => o.PayoutSplit)
            .Include(o => o.DeliverySession)
            .Include(o => o.Dispute)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<Order?> GetByIdWithTimelineAsync(Guid id, CancellationToken ct)
    {
        return await _context.Orders
            .Include(o => o.Merchant)
            .Include(o => o.Buyer)
            .Include(o => o.EscrowTransaction)
            .Include(o => o.DeliverySession)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<Order?> GetByIdWithBuyerAndSessionAsync(Guid id, CancellationToken ct)
    {
        return await _context.Orders
            .Include(o => o.Buyer)
            .Include(o => o.DeliverySession)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<Order?> GetByIdForPaymentAsync(Guid id, CancellationToken ct)
    {
        return await _context.Orders
            .Include(o => o.Merchant)
            .Include(o => o.EscrowTransaction)
            .FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    public async Task<(List<Order> Items, int TotalCount)> GetMerchantOrdersAsync(Guid merchantId, int pageNumber, int pageSize, OrderStatus? statusFilter, CancellationToken ct)
    {
        var query = _context.Orders
            .Include(o => o.Buyer)
            .Where(o => o.MerchantId == merchantId)
            .AsNoTracking();

        if (statusFilter.HasValue)
            query = query.Where(o => o.Status == statusFilter.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public void Add(Order order)
    {
        _context.Orders.Add(order);
    }
}
