using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Infrastructure.Persistence.Repositories;

public class EscrowTransactionRepository : IEscrowTransactionRepository
{
    private readonly IApplicationDbContext _context;

    public EscrowTransactionRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EscrowTransaction?> GetByAlatPayTransactionIdAsync(string transactionId, CancellationToken ct)
    {
        return await _context.EscrowTransactions
            .Include(e => e.Order)
            .FirstOrDefaultAsync(e => e.AlatPayTransactionId == transactionId, ct);
    }

    public void Add(EscrowTransaction transaction)
    {
        _context.EscrowTransactions.Add(transaction);
    }
}
