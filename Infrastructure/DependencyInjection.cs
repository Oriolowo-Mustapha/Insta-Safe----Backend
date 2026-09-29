using InstaSafe.Application;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Infrastructure.ExternalServices;
using InstaSafe.Infrastructure.Outbox;
using InstaSafe.Infrastructure.Persistence;
using InstaSafe.Infrastructure.Repositories;
using InstaSafe.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddApplication();

        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("Default")
                ?? config["ConnectionStrings:Default"]
                ?? "Host=localhost;Port=5432;Database=instasafe;Username=postgres;Password=postgres"));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IVendorRepository, VendorRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IDispatcherRepository, DispatcherRepository>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IEmailSender, BrevoEmailSender>();
        services.AddSingleton<ISanitizer, HtmlSanitizerAdapter>();
        services.AddSingleton<IOtpService, OtpService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddHttpClient<IPaystackClient, PaystackClient>();
        services.AddHttpClient<IGroqParser, GroqParser>();
        services.AddHttpClient<IContactResolver, OpenWAContactResolver>();
        services.AddHttpClient<WhatsAppSender>();
        services.AddScoped<IWhatsAppSender>(sp =>
            new LoggingWhatsAppSender(
                sp.GetRequiredService<WhatsAppSender>(),
                sp.GetRequiredService<IAppDbContext>(),
                sp.GetRequiredService<ILogger<LoggingWhatsAppSender>>()));

        services.AddHostedService<OutboxPublisher>();
        services.AddHostedService<ReleaseDueOrdersWorker>();

        return services;
    }
}
