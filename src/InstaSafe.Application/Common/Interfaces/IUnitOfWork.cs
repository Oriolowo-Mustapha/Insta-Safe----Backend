using InstaSafe.Domain.Common;

namespace InstaSafe.Application.Common.Interfaces;

public interface IUnitOfWork
{
    IOrderRepository Orders { get; }
    IBuyerRepository Buyers { get; }
    IEscrowTransactionRepository EscrowTransactions { get; }
    IDeliverySessionRepository DeliverySessions { get; }
    IWebhookEventLogRepository WebhookEventLogs { get; }
    IGenericRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
