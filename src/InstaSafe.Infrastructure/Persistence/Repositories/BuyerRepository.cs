using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstaSafe.Infrastructure.Persistence.Repositories;

public class BuyerRepository : IBuyerRepository
{
    private readonly IApplicationDbContext _context;

    public BuyerRepository(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Buyer> GetOrCreateAsync(string email, string firstName, string lastName, string phone, CancellationToken ct)
    {
        var existing = await _context.Buyers
            .AsTracking()
            .FirstOrDefaultAsync(b => b.Email == email, ct);

        if (existing is not null)
            return existing;

        var buyer = new Buyer
        {
            Id = Guid.NewGuid(),
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Phone = phone,
            CreatedAt = DateTime.UtcNow
        };

        _context.Buyers.Add(buyer);
        return buyer;
    }
}
