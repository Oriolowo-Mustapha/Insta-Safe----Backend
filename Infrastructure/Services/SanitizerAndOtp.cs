using Ganss.Xss;
using InstaSafe.Application.Common.Interfaces;
using System.Text.RegularExpressions;

namespace InstaSafe.Infrastructure.Services;

public class HtmlSanitizerAdapter : ISanitizer
{
    private readonly HtmlSanitizer _sanitizer = new();

    public string Clean(string? input, int maxLength = 2000)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var sanitized = _sanitizer.Sanitize(input);
        sanitized = Regex.Replace(sanitized, @"\s+", " ").Trim();
        sanitized = new string(sanitized.Where(c => !char.IsControl(c) || c is '\n' or '\r' or '\t').ToArray());
        return sanitized.Length > maxLength ? sanitized[..maxLength].Trim() : sanitized;
    }
}

public class OtpService : IOtpService
{
    public string GenerateOtp(int digits = 6)
    {
        var rnd = Random.Shared;
        var min = (int)Math.Pow(10, digits - 1);
        return rnd.Next(min, min * 10 - 1).ToString($"D{digits}");
    }

    public string NewSalt() => Guid.NewGuid().ToString("N")[..16];

    public string Hash(string otp, string salt)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"{salt}:{otp}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
