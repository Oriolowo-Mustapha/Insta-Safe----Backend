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

    public async Task<EscrowTransaction?> GetByMonnifyTransactionReferenceAsync(string monnifyReference, CancellationToken ct)
    {
        return await _context.EscrowTransactions
            .FirstOrDefaultAsync(e => e.MonnifyTransactionReference == monnifyReference, ct);
    }

    public async Task<EscrowTransaction?> GetByTransactionReferenceAsync(string transactionReference, CancellationToken ct)
    {
        return await _context.EscrowTransactions
            .FirstOrDefaultAsync(e => e.TransactionReference == transactionReference, ct);
    }

    public void Add(EscrowTransaction transaction)
    {
        _context.EscrowTransactions.Add(transaction);
    }
}
