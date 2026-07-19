using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models.Monnify;
using InstaSafe.Infrastructure.Delivery;

using InstaSafe.Infrastructure.ExternalServices.Monnify;
using InstaSafe.Infrastructure.ExternalServices.Cloudinary;
using InstaSafe.Infrastructure.ExternalServices.OpenRouter;
using InstaSafe.Infrastructure.ExternalServices.WhatsApp;
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

        services.Configure<MonnifyOptions>(configuration.GetSection(MonnifyOptions.SectionName));
        services.AddMemoryCache();
        services.AddHttpClient<IMonnifyPaymentService, MonnifyClient>()
            .AddTransientHttpErrorPolicy(policyBuilder =>
                policyBuilder.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        services.Configure<QrOptions>(configuration.GetSection(QrOptions.SectionName));
        services.AddScoped<IQrTokenService, QrTokenService>();
        services.AddScoped<IFingerprintMatcher, FingerprintMatcher>();
        services.AddScoped<IFraudScoringEngine, FraudScoringEngine>();

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IInAppNotificationService, InAppNotificationService>();
        services.AddHttpClient<IEmailService, BrevoEmailService>();
        
        services.Configure<CloudinaryOptions>(configuration.GetSection(CloudinaryOptions.SectionName));
        services.AddScoped<IImageUploadService, CloudinaryImageUploadService>();

        services.Configure<OpenRouterOptions>(configuration.GetSection(OpenRouterOptions.SectionName));
        services.AddHttpClient<IDisputeResolutionAiService, OpenRouterAiService>()
            .AddTransientHttpErrorPolicy(policyBuilder =>
                policyBuilder.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
                
        services.AddHttpClient<IChatbotAiService, ChatbotAiService>()
            .AddTransientHttpErrorPolicy(policyBuilder =>
                policyBuilder.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
                
        services.Configure<OpenWaOptions>(configuration.GetSection(OpenWaOptions.SectionName));
        services.AddHttpClient<IWhatsAppMessagingService, OpenWaMessagingService>()
            .AddTransientHttpErrorPolicy(policyBuilder =>
                policyBuilder.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

        // Hangfire
        services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(configuration.GetConnectionString("DefaultConnection")));

        services.AddHangfireServer();

        return services;
    }
}
