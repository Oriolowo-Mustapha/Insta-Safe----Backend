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

    /// <summary>
    /// Input scrutiny for phone answers. Normalize only strips formatting, so
    /// length alone accepts any prose ("the customer phone is abc" normalizes
    /// to 22 chars). A phone answer must actually be digits of a plausible
    /// length. Nigerian mobiles are 10-13 digits; 7-15 stays lenient for
    /// short codes and international formats while blocking sentences.
    /// </summary>
    public static bool LooksLikePhone(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var s = Normalize(raw);
        return s.Length >= 7 && s.Length <= 15 && s.All(char.IsDigit);
    }
}
