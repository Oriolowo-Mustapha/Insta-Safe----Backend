using InstaSafe.Domain.Entities;
using InstaSafe.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Infrastructure.Persistence;

public class ApplicationDbContextSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ApplicationDbContextSeeder> _logger;

    public ApplicationDbContextSeeder(
        ApplicationDbContext context, 
        IPasswordHasher passwordHasher,
        ILogger<ApplicationDbContextSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            if (_context.Database.IsNpgsql())
            {
                await _context.Database.MigrateAsync();
            }

            await SeedRolesAsync();
            await SeedAdminUserAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while seeding the database.");
            throw;
        }
    }

    private async Task SeedRolesAsync()
    {
        var roles = new List<string> { "Admin", "Merchant" };

        foreach (var roleName in roles)
        {
            if (!await _context.Roles.AnyAsync(r => r.Name == roleName))
            {
                _context.Roles.Add(new Role
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    Description = $"{roleName} Role"
                });
            }
        }

        await _context.SaveChangesAsync();
    }

    private async Task SeedAdminUserAsync()
    {
        var adminEmail = "admin@instasafe.com";
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");

        if (adminRole != null && !await _context.Users.AnyAsync(u => u.Email == adminEmail))
        {
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                FirstName = "System",
                LastName = "Admin",
                Email = adminEmail,
                Phone = "00000000000",
                IsActive = true,
                PasswordHash = _passwordHasher.Hash("Admin@123")
            };

            adminUser.VerifyEmail();

            _context.Users.Add(adminUser);
            
            _context.UserRoles.Add(new UserRole 
            { 
                UserId = adminUser.Id, 
                RoleId = adminRole.Id 
            });

            await _context.SaveChangesAsync();
            _logger.LogInformation("Admin user seeded successfully. Email: admin@instasafe.com | Password: Admin@123");
        }
    }
}
