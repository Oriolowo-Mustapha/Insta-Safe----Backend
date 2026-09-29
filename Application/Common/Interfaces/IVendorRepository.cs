using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Common.Interfaces;

public interface IVendorRepository
{
    Task<Vendor?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Vendor?> GetByPhoneAsync(string phone, CancellationToken ct);
    Task<Vendor?> GetByEmailAsync(string email, CancellationToken ct);
    Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct);
    Task AddAsync(Vendor vendor, CancellationToken ct);
    Task<List<Vendor>> ListAsync(int page, int pageSize, bool? activeOnly, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
