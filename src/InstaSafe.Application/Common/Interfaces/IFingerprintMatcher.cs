namespace InstaSafe.Application.Common.Interfaces;

public interface IFingerprintMatcher
{
    bool IsMatch(string pickupFingerprint, string deliveryFingerprint, double threshold = 0.9);
}
