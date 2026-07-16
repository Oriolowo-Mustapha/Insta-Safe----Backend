using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models;
using Microsoft.Extensions.Options;

namespace InstaSafe.Infrastructure.Delivery;

public class QrTokenService : IQrTokenService
{
    private readonly QrOptions _options;

    public QrTokenService(IOptions<QrOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateSignedToken(QrPayload payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var payloadBase64 = Convert.ToBase64String(payloadBytes);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningKey));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64));
        var signature = Convert.ToBase64String(signatureBytes);

        return $"{payloadBase64}.{signature}";
    }

    public QrPayload? ValidateAndDecodeToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        var parts = token.Split('.');
        if (parts.Length != 2)
            return null;

        var payloadBase64 = parts[0];
        var signature = parts[1];

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.SigningKey));
        var expectedSignature = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64)));

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature),
                Encoding.UTF8.GetBytes(expectedSignature)))
            return null;

        try
        {
            var payloadBytes = Convert.FromBase64String(payloadBase64);
            var payloadJson = Encoding.UTF8.GetString(payloadBytes);
            var payload = JsonSerializer.Deserialize<QrPayload>(payloadJson);

            if (payload == null)
                return null;

            if (payload.ExpiresAt < DateTime.UtcNow)
                return null;

            if (payload.IssuedAt > DateTime.UtcNow)
                return null;

            return payload;
        }
        catch
        {
            return null;
        }
    }
}
