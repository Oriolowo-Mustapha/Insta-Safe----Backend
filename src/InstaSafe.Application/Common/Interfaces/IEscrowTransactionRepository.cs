using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Common.Interfaces;

public interface IEscrowTransactionRepository
{
    Task<EscrowTransaction?> GetByAlatPayTransactionIdAsync(string transactionId, CancellationToken ct);
    void Add(EscrowTransaction transaction);
}
