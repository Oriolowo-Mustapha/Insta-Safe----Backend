using InstaSafe.Application.Common.Helpers;

namespace InstaSafe.Infrastructure.ExternalServices;

public static class ChatAudit
{
    public const int MaxBodyLength = 1000;

    public static string Normalize(string raw) => PhoneNormalizer.Normalize(raw);

    public static string Truncate(string? body)
    {
        if (string.IsNullOrEmpty(body)) return string.Empty;
        return body.Length > MaxBodyLength ? body[..MaxBodyLength] : body;
    }
}
