using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Common.Interfaces;

public interface IEscrowTransactionRepository
{
    Task<EscrowTransaction?> GetByMonnifyTransactionReferenceAsync(string monnifyTransactionReference, CancellationToken ct);
    Task<EscrowTransaction?> GetByTransactionReferenceAsync(string transactionReference, CancellationToken ct);
    void Add(EscrowTransaction transaction);
}
