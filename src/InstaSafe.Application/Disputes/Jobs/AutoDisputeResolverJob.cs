using InstaSafe.Application.Common.Interfaces;
using InstaSafe.Application.Disputes.Commands.ResolveDispute;
using InstaSafe.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InstaSafe.Application.Disputes.Jobs;

public class AutoDisputeResolverJob : IAutoDisputeResolverJob
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDisputeResolutionAiService _aiService;
    private readonly ISender _sender;
    private readonly ILogger<AutoDisputeResolverJob> _logger;

    public AutoDisputeResolverJob(IUnitOfWork unitOfWork, IDisputeResolutionAiService aiService, ISender sender, ILogger<AutoDisputeResolverJob> logger)
    {
        _unitOfWork = unitOfWork;
        _aiService = aiService;
        _sender = sender;
        _logger = logger;
    }

    public async Task ProcessAsync(Guid disputeId)
    {
        var dispute = await _unitOfWork.Repository<Dispute>().GetByIdAsync(disputeId, CancellationToken.None);
        if (dispute == null || dispute.Status != Domain.Enums.DisputeStatus.Open)
        {
            return;
        }

        var order = await _unitOfWork.Orders.GetByIdWithAllAsync(dispute.OrderId, CancellationToken.None);
        if (order == null || string.IsNullOrWhiteSpace(dispute.EvidenceUrls))
        {
            return;
        }

        try
        {
            _logger.LogInformation("Starting AI Dispute Resolution for Dispute: {DisputeId}", disputeId);

            var urls = dispute.EvidenceUrls.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

            var aiResult = await _aiService.AnalyzeDisputeEvidenceAsync(order.ItemDescription ?? order.ItemName, urls, CancellationToken.None);

            if (aiResult.ConfidenceScore >= 80)
            {
                var resolutionStr = aiResult.SuggestedResolution == "RefundBuyer" ? "refund" : "release";
                var adminNotes = $"[AI Auto-Resolved - Confidence: {aiResult.ConfidenceScore}%] {aiResult.Reasoning}";

                var command = new ResolveDisputeCommand(disputeId, resolutionStr, adminNotes, "SYSTEM_AI");

                var result = await _sender.Send(command);

                if (result.Succeeded)
                {
                    _logger.LogInformation("AI successfully auto-resolved dispute {DisputeId} with action {Action}", disputeId, resolutionStr);
                }
                else
                {
                    _logger.LogWarning("AI resolution for dispute {DisputeId} failed during command execution: {Error}", disputeId, string.Join(", ", result.Errors));
                }
            }
            else
            {
                _logger.LogInformation("AI Confidence ({Score}%) below threshold for dispute {DisputeId}. Requires manual review.", aiResult.ConfidenceScore, disputeId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during AutoDisputeResolverJob for Dispute: {DisputeId}", disputeId);
        }
    }
}
