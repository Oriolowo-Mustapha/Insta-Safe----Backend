using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Infrastructure.Delivery;
using InstaSafe.Infrastructure.ExternalServices.AlatPay;
using InstaSafe.Infrastructure.Persistence;
using InstaSafe.Infrastructure.Persistence.Repositories;
using InstaSafe.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Hangfire;
using Hangfire.PostgreSql;

namespace InstaSafe.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                builder => builder.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<ApplicationDbContextSeeder>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddTransient<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddHttpContextAccessor();

        services.Configure<AlatPayOptions>(configuration.GetSection(AlatPayOptions.SectionName));

        services.AddHttpClient<IAlatPayClient, AlatPayClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AlatPayOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", options.SubscriptionKey);
            client.DefaultRequestHeaders.Add("Authorization", options.PrimaryKey);
        })
        .AddTransientHttpErrorPolicy(policyBuilder =>
            policyBuilder.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        services.Configure<QrOptions>(configuration.GetSection(QrOptions.SectionName));
        services.AddScoped<IQrTokenService, QrTokenService>();
        services.AddScoped<IFingerprintMatcher, FingerprintMatcher>();

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IInAppNotificationService, InAppNotificationService>();
        services.AddHttpClient<IEmailService, BrevoEmailService>();

        // Hangfire
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(configuration.GetConnectionString("DefaultConnection")));

        services.AddHangfireServer();

        return services;
    }
}
