namespace InstaSafe.Application.Common.Helpers;

public static class PhoneNormalizer
{
    public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var s = raw.Trim();
        var at = s.IndexOf('@');
        if (at >= 0) s = s[..at];
        s = s.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        if (s.StartsWith("+")) s = s[1..];
        return s;
    }
}
