using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace InstaSafe.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> commands can create
/// <see cref="AppDbContext"/> without booting the API host.
/// Connection string resolution order:
/// <c>ConnectionStrings__Default</c> env var → API appsettings → local fallback.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string FallbackConnectionString =
        "Host=localhost;Port=5432;Database=instasafe;Username=postgres;Password=postgres";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // Uses the design-time ctor (no IPublisher); domain events are
        // simply cleared after SaveChanges when no publisher is present.
        return new AppDbContext(options);
    }

    private static string ResolveConnectionString()
    {
        DotNetEnv.Env.TraversePath().Load();
        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

        var apiSettings = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "API", "appsettings.json");

        if (File.Exists(apiSettings))
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(apiSettings, optional: true)
                .AddEnvironmentVariables()
                .Build();

            var fromConfig = config.GetConnectionString("Default");
            if (!string.IsNullOrWhiteSpace(fromConfig)) return fromConfig;
        }

        return FallbackConnectionString;
    }
}
