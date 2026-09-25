using DotNetEnv;
using InstaSafe.Api.Middleware;
using InstaSafe.Infrastructure;
using InstaSafe.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Formatting.Compact;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using System.Text;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .Enrich.WithMachineName()
       .Enrich.WithEnvironmentName();

    // Render/Railway-style: single-line JSON in production, human-readable locally.
    if (ctx.HostingEnvironment.IsDevelopment())
        cfg.WriteTo.Console();
    else
        cfg.WriteTo.Console(new CompactJsonFormatter());

    // Application Insights when configured (Azure). Absent locally = no-op.
    var aiConnection = ctx.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
    if (!string.IsNullOrWhiteSpace(aiConnection))
        cfg.WriteTo.ApplicationInsights(aiConnection, new TraceTelemetryConverter());

    // Windows App Service swallows console stdout (generated web.config sets
    // stdoutLogEnabled=false), so also log to LogFiles where Log Stream tails.
    // Active only on Azure (WEBSITE_SITE_NAME) unless Serilog:FileDir overrides.
    var fileDir = ctx.Configuration["Serilog:FileDir"];
    if (!string.IsNullOrWhiteSpace(fileDir) || Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") is not null)
    {
        var dir = string.IsNullOrWhiteSpace(fileDir)
            ? Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? ".", "LogFiles", "Application")
            : fileDir;
        cfg.WriteTo.File(new CompactJsonFormatter(), Path.Combine(dir, "instasafe-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            shared: true);
    }
});

builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(o =>
{
	o.SwaggerDoc("v1", new() { Title = "InstaSafe API", Version = "v1" });
});

builder.Services.AddInfrastructure(builder.Configuration);

var jwtKey = builder.Configuration["Auth:JwtKey"] ?? "";
var jwtIssuer = builder.Configuration["Auth:JwtIssuer"] ?? "instasafe";
var jwtAudience = builder.Configuration["Auth:JwtAudience"] ?? "instasafe-vendors";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                jwtKey.Length >= 32 ? jwtKey : new string('0', 32))),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Azure App Service has no local migration step: set ApplyMigrations=true
// in App Settings once and the schema (all EF migrations) is created on boot.
if (string.Equals(builder.Configuration["ApplyMigrations"], "true", StringComparison.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestCorrelationMiddleware>();
app.UseSerilogRequestLogging(o =>
    o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.00} ms [{RequestId}]");

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "InstaSafe API v1");
    });
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "instasafe" }));

app.Run();
