using System.Security.Cryptography;

namespace InstaSafe.Application.Common.Helpers;

/// <summary>
/// Customer-facing order numbers: IS- + 6 unambiguous chars
/// (no 0/O, 1/I/L). Stable across payment rails, unlike Paystack refs.
/// </summary>
public static class OrderNumberGenerator
{
    public const string Prefix = "IS-";
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(6);
        var chars = new char[6];
        for (var i = 0; i < 6; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        return Prefix + new string(chars);
    }
}
