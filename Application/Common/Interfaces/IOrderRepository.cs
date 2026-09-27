using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Common.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Order?> GetByPaystackRefAsync(string reference, CancellationToken ct);
    Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct);
    Task<List<Order>> ListUnpaidByEmailAsync(string email, CancellationToken ct);
    Task AddAsync(Order order, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<List<Order>> ListAsync(int page, int pageSize, CancellationToken ct);
    Task<List<Order>> ListByVendorAsync(Guid vendorId, string vendorPhone, int page, int pageSize, CancellationToken ct);
    Task<List<Order>> ListByDriverAsync(Guid driverId, string driverPhone, int page, int pageSize, CancellationToken ct);
}
