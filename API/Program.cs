using DotNetEnv;
using InstaSafe.Api.Middleware;
using InstaSafe.Infrastructure;
using InstaSafe.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console());

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(o =>
{
	o.CustomSchemaIds(type => type.FullName);
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
