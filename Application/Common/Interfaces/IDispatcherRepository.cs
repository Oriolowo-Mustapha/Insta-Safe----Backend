using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Common.Interfaces;

public interface IDispatcherRepository
{
    Task<Dispatcher?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Dispatcher?> GetByPhoneAsync(string phone, CancellationToken ct);
    Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct);
    Task AddAsync(Dispatcher dispatcher, CancellationToken ct);
    Task<List<Dispatcher>> ListAsync(int page, int pageSize, bool? activeOnly, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
