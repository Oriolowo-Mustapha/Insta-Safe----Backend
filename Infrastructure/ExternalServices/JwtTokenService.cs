using InstaSafe.Application.Common.Helpers;
using InstaSafe.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace InstaSafe.Infrastructure.ExternalServices;

public class JwtTokenService : IJwtTokenService
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly double _hours;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IConfiguration config, ILogger<JwtTokenService> logger)
    {
        _key = config["Auth:JwtKey"] ?? string.Empty;
        _issuer = config["Auth:Issuer"] ?? config["Auth:JwtIssuer"] ?? "instasafe";
        _audience = config["Auth:Audience"] ?? config["Auth:JwtAudience"] ?? "instasafe-vendors";
        _hours = double.TryParse(config["Auth:TokenHours"], out var h) && h > 0 ? h : 24;
        _logger = logger;
    }

    public string CreateVendorToken(Guid vendorId, string phone) =>
        CreateToken(
            [new Claim(ClaimsPrincipalExtensions.VendorIdClaim, vendorId.ToString())],
            phone, "vendor");

    public string CreateDispatcherToken(Guid dispatcherId, string phone) =>
        CreateToken(
            [new Claim(ClaimsPrincipalExtensions.DispatcherIdClaim, dispatcherId.ToString())],
            phone, "dispatcher");

    private string CreateToken(List<Claim> idClaims, string phone, string role)
    {
        if (_key.Length < 32)
        {
            _logger.LogWarning("Auth:JwtKey is missing or too short; token will not validate until configured.");
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _key.Length >= 32 ? _key : new string('0', 32)));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>(idClaims)
        {
            new(ClaimTypes.MobilePhone, phone),
            new(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_hours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
