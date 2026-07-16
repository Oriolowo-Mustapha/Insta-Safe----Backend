using InstaSafe.Application.Common.Interfaces;

namespace InstaSafe.Infrastructure.Delivery;

public class FingerprintMatcher : IFingerprintMatcher
{
    public bool IsMatch(string pickupFingerprint, string deliveryFingerprint, double threshold = 0.9)
    {
        if (string.IsNullOrEmpty(pickupFingerprint) || string.IsNullOrEmpty(deliveryFingerprint))
            return false;

        if (string.Equals(pickupFingerprint, deliveryFingerprint, StringComparison.Ordinal))
            return true;

        var similarity = ComputeSimilarity(pickupFingerprint, deliveryFingerprint);
        return similarity >= threshold;
    }

    private static double ComputeSimilarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0)
            return 1.0;

        if (a.Length == 0 || b.Length == 0)
            return 0.0;

        var maxLen = Math.Max(a.Length, b.Length);
        if (maxLen == 0)
            return 1.0;

        var distance = LevenshteinDistance(a.AsSpan(), b.AsSpan());
        return 1.0 - (double)distance / maxLen;
    }

    private static int LevenshteinDistance(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
