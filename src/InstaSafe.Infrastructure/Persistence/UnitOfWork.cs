using System.Collections;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Common;
using InstaSafe.Infrastructure.Persistence.Repositories;

namespace InstaSafe.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private Hashtable? _repositories;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
        Orders = new OrderRepository(context);
        Buyers = new BuyerRepository(context);
        EscrowTransactions = new EscrowTransactionRepository(context);
        DeliverySessions = new DeliverySessionRepository(context);
        WebhookEventLogs = new WebhookEventLogRepository(context);
    }

    public IOrderRepository Orders { get; }
    public IBuyerRepository Buyers { get; }
    public IEscrowTransactionRepository EscrowTransactions { get; }
    public IDeliverySessionRepository DeliverySessions { get; }
    public IWebhookEventLogRepository WebhookEventLogs { get; }

    public IGenericRepository<T> Repository<T>() where T : BaseEntity
    {
        _repositories ??= new Hashtable();

        var type = typeof(T).Name;

        if (!_repositories.ContainsKey(type))
        {
            var repositoryInstance = new GenericRepository<T>(_context);
            _repositories.Add(type, repositoryInstance);
        }

        return (IGenericRepository<T>)_repositories[type]!;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
