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
        // Canonical Nigerian mobile form: 0803... (11 digits) -> 234803...
        // so web-typed, +234, and gateway-resolved numbers all match.
        if (s.Length == 11 && s[0] == '0' && s.All(char.IsDigit))
            s = "234" + s[1..];
        return s;
    }
}
