namespace InstaSafe.Application.Common.Helpers;

public static class EmailChecker
{
    public static bool IsPlausible(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        var e = email.Trim();
        if (e.Contains(' ') || e.Length > 200) return false;
        var at = e.IndexOf('@');
        if (at <= 0 || at != e.LastIndexOf('@')) return false;
        var domain = e[(at + 1)..];
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.');
    }
}
