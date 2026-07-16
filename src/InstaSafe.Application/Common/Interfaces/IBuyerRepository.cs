using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Common.Interfaces;

public interface IBuyerRepository
{
    Task<Buyer> GetOrCreateAsync(string email, string firstName, string lastName, string phone, CancellationToken ct);
}
