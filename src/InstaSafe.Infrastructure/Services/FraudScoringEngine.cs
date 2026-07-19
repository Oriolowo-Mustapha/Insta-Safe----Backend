using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Common.Models.Monnify;
using InstaSafe.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Infrastructure.Services;

public class FraudScoringEngine : IFraudScoringEngine
{
    private readonly IMonnifyPaymentService _monnifyService;
    private readonly ILogger<FraudScoringEngine> _logger;

    public FraudScoringEngine(IMonnifyPaymentService monnifyService, ILogger<FraudScoringEngine> logger)
    {
        _monnifyService = monnifyService;
        _logger = logger;
    }

    public async Task<FraudScoreResult> EvaluateMerchantRiskAsync(Merchant merchant, CancellationToken cancellationToken = default)
    {
        int score = 100; // Start at max risk
        var factors = new List<string>();

        // 1. Check Bank Account
        if (!string.IsNullOrEmpty(merchant.PayoutBankAccount) && !string.IsNullOrEmpty(merchant.PayoutBankCode))
        {
            try
            {
                var accountResponse = await _monnifyService.VerifyAccountAsync(merchant.PayoutBankAccount, merchant.PayoutBankCode, cancellationToken);
                if (accountResponse.RequestSuccessful && accountResponse.ResponseBody != null)
                {
                    // Account verified
                    score -= 40;
                    factors.Add("Bank Account Verified");
                }
                else
                {
                    factors.Add("Bank Account Verification Failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to verify bank account for merchant {MerchantId}", merchant.Id);
                factors.Add("Bank Account Verification Error");
            }
        }
        else
        {
            factors.Add("No Bank Account Provided");
        }

        // 2. Check BVN
        if (!string.IsNullOrEmpty(merchant.Bvn))
        {
            if (string.IsNullOrEmpty(merchant.LegalFirstName) || string.IsNullOrEmpty(merchant.LegalLastName) || !merchant.DateOfBirth.HasValue)
            {
                factors.Add("BVN Verification Skipped (Missing Legal Name or DOB)");
            }
            else
            {
                try
                {
                    var request = new BvnMatchRequest(
                        merchant.Bvn,
                        $"{merchant.LegalFirstName} {merchant.LegalLastName}",
                        merchant.DateOfBirth.Value.ToString("yyyy-MM-dd"),
                        merchant.Phone);

                    var bvnResponse = await _monnifyService.VerifyBvnAsync(request, cancellationToken);
                    
                    if (bvnResponse.RequestSuccessful)
                    {
                        score -= 30;
                        factors.Add("BVN Verified");
                    }
                    else
                    {
                        factors.Add("BVN Verification Failed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to verify BVN for merchant {MerchantId}", merchant.Id);
                    factors.Add("BVN Verification Error");
                }
            }
        }
        else
        {
            factors.Add("No BVN Provided");
        }

        // 3. Check NIN
        if (!string.IsNullOrEmpty(merchant.Nin))
        {
            if (string.IsNullOrEmpty(merchant.LegalFirstName) || string.IsNullOrEmpty(merchant.LegalLastName) || !merchant.DateOfBirth.HasValue)
            {
                factors.Add("NIN Verification Skipped (Missing Legal Name or DOB)");
            }
            else
            {
                try
                {
                    var request = new NinVerificationRequest(
                        merchant.Nin,
                        merchant.LegalFirstName,
                        merchant.LegalLastName,
                        merchant.DateOfBirth.Value.ToString("yyyy-MM-dd"));

                    var ninResponse = await _monnifyService.VerifyNinAsync(request, cancellationToken);
                    
                    if (ninResponse.RequestSuccessful)
                    {
                        score -= 30;
                        factors.Add("NIN Verified");
                    }
                    else
                    {
                        factors.Add("NIN Verification Failed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to verify NIN for merchant {MerchantId}", merchant.Id);
                    factors.Add("NIN Verification Error");
                }
            }
        }
        else
        {
            factors.Add("No NIN Provided");
        }

        // Ensure score bounds
        score = Math.Clamp(score, 0, 100);

        string riskLevel = score switch
        {
            < 30 => "Low",
            <= 70 => "Medium",
            _ => "High"
        };

        return new FraudScoreResult(score, riskLevel, factors.ToArray());
    }
}
