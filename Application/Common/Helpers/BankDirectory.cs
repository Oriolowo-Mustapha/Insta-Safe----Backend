using InstaSafe.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Common.Helpers;

public sealed record BankInfo(string Name, string Slug, string Code);

/// <summary>
/// Matches free-text bank names ("GTB", "Guaranty Trust") to Paystack banks.
/// List cached 24h; matching is deterministic and unit-testable.
/// </summary>
public class BankDirectory
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gtb"] = "Guaranty Trust Bank",
        ["gtbank"] = "Guaranty Trust Bank",
        ["gt bank"] = "Guaranty Trust Bank",
        ["uba"] = "United Bank For Africa",
        ["firstbank"] = "First Bank of Nigeria",
        ["first bank"] = "First Bank of Nigeria",
        ["zenith"] = "Zenith Bank",
        ["access"] = "Access Bank",
        ["diamond"] = "Access Bank",
        ["fidelity"] = "Fidelity Bank",
        ["fcmb"] = "First City Monument Bank",
        ["sterling"] = "Sterling Bank",
        ["union"] = "Union Bank of Nigeria",
        ["wema"] = "Wema Bank",
        ["alat"] = "Wema Bank",
        ["stanbic"] = "Stanbic IBTC Bank",
        ["ibtc"] = "Stanbic IBTC Bank",
        ["heritage"] = "Heritage Bank",
        ["keystone"] = "Keystone Bank",
        ["unity"] = "Unity Bank",
        ["jaiz"] = "Jaiz Bank",
        ["taj"] = "Taj Bank",
        ["globus"] = "Globus Bank",
        ["providus"] = "Providus Bank",
        ["suntrust"] = "SunTrust Bank",
        ["polaris"] = "Polaris Bank",
        ["skye"] = "Polaris Bank",
        ["ecobank"] = "Ecobank Nigeria",
        ["fidelity bank"] = "Fidelity Bank",
        ["kuda"] = "Kuda Bank",
        ["opay"] = "OPay",
        ["palmpay"] = "PalmPay",
        ["moniepoint"] = "Moniepoint",
        ["paystack-titan"] = "Paystack-Titan",
        ["titan"] = "Paystack-Titan",
    };

    private readonly IPaystackClient _paystack;
    private readonly ILogger<BankDirectory> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<BankInfo> _cache = [];
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    public BankDirectory(IPaystackClient paystack, ILogger<BankDirectory> logger)
    {
        _paystack = paystack; _logger = logger;
    }

    public async Task<IReadOnlyList<BankInfo>> GetBanksAsync(CancellationToken ct)
    {
        if (DateTimeOffset.UtcNow - _cachedAt < Ttl && _cache.Count > 0)
            return _cache;
        await _gate.WaitAsync(ct);
        try
        {
            if (DateTimeOffset.UtcNow - _cachedAt < Ttl && _cache.Count > 0)
                return _cache;
            var banks = await _paystack.ListAllBanksAsync(ct);
            _cache = banks.Select(b => new BankInfo(b.Name, b.Slug, b.Code)).ToList();
            _cachedAt = DateTimeOffset.UtcNow;
            return _cache;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bank list refresh failed; using cache");
            return _cache;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Best single match for free text, or null when none/ambiguous.</summary>
    public static BankInfo? Match(string input, IEnumerable<BankInfo> banks)
    {
        var text = (input ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(text)) return null;
        var list = banks.ToList();
        if (list.Count == 0) return null;

        var digits = new string(text.Where(char.IsDigit).ToArray());
        if (digits.Length == 3)
        {
            var byCode = list.FirstOrDefault(b => b.Code == digits);
            if (byCode is not null) return byCode;
        }

        if (Aliases.TryGetValue(text, out var aliased))
        {
            var hit = list.FirstOrDefault(b => WordsContain(b.Name, aliased));
            if (hit is not null) return hit;
        }

        var norm = text.Replace("bank", "").Replace("  ", " ").Trim();
        var scored = list
            .Select(b => (Bank: b, Score: Score(norm, b)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();
        if (scored.Count == 0) return null;
        if (scored.Count > 1 && scored[0].Score == scored[1].Score) return null;
        return scored[0].Bank;
    }

    private static bool WordsContain(string name, string phrase)
    {
        static string[] Words(string s) => s.ToLowerInvariant()
            .Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        var n = Words(name);
        var p = Words(phrase);
        var (shorter, longer) = p.Length <= n.Length ? (p, n) : (n, p);
        return shorter.All(w => longer.Contains(w));
    }

    private static int Score(string norm, BankInfo bank)
    {
        var name = bank.Name.ToLowerInvariant();
        var slug = bank.Slug.ToLowerInvariant();
        if (name == norm || slug == norm.Replace(" ", "-")) return 100;
        if (slug.StartsWith(norm.Replace(" ", "-")) && norm.Length >= 4) return 80;
        if (name.StartsWith(norm) && norm.Length >= 4) return 70;
        if (norm.Length >= 5 && name.Contains(norm)) return 50;
        return 0;
    }
}
