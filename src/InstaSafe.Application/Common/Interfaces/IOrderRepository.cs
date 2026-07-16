using InstaSafe.Domain.Entities;
using InstaSafe.Domain.Enums;

namespace InstaSafe.Application.Common.Interfaces;

public interface IOrderRepository
{
    Task<bool> MerchantExistsAsync(Guid merchantId, CancellationToken ct);
    Task<int> CountOrdersByReferencePrefixAsync(string prefix, CancellationToken ct);
    Task<Order?> GetByIdWithMerchantAsync(Guid id, CancellationToken ct);
    Task<Order?> GetByIdWithEscrowAsync(Guid id, CancellationToken ct);
    Task<Order?> GetByIdWithAllAsync(Guid id, CancellationToken ct);
    Task<Order?> GetByIdWithTimelineAsync(Guid id, CancellationToken ct);
    Task<Order?> GetByIdWithBuyerAndSessionAsync(Guid id, CancellationToken ct);
    Task<Order?> GetByIdForPaymentAsync(Guid id, CancellationToken ct);
    Task<(List<Order> Items, int TotalCount)> GetMerchantOrdersAsync(Guid merchantId, int pageNumber, int pageSize, OrderStatus? statusFilter, CancellationToken ct);
    void Add(Order order);
}
