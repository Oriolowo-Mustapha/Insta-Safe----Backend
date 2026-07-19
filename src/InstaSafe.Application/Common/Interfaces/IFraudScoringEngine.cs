using InstaSafe.Domain.Entities;

namespace InstaSafe.Application.Common.Interfaces;

public record FraudScoreResult(int Score, string RiskLevel, string[] Factors);

public interface IFraudScoringEngine
{
    /// <summary>
    /// Calculates a fraud risk score (0-100) for a merchant.
    /// Lower score is better (0 = No Risk, 100 = High Risk).
    /// </summary>
    Task<FraudScoreResult> EvaluateMerchantRiskAsync(Merchant merchant, CancellationToken cancellationToken = default);
}
