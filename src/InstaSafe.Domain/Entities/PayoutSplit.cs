using InstaSafe.Domain.Common;
using InstaSafe.Domain.Enums;

namespace InstaSafe.Domain.Entities;

public class PayoutSplit : BaseEntity
{
    public Guid OrderId { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal MerchantAmount { get; init; }
    public decimal PlatformCommission { get; init; }
    public decimal CommissionRate { get; init; }
    public DateTime? ExecutedAt { get; private set; }
    public string? MonnifyTransferReference { get; private set; }
    public PayoutStatus Status { get; private set; } = PayoutStatus.Pending;

    public virtual Order Order { get; private set; } = null!;

    public void MarkAsProcessing()
    {
        Status = PayoutStatus.Processing;
    }

    public void MarkAsCompleted(string reference, DateTime completedAt)
    {
        Status = PayoutStatus.Completed;
        MonnifyTransferReference = reference;
        ExecutedAt = completedAt;
    }

    public void MarkAsFailed()
    {
        Status = PayoutStatus.Failed;
    }
}
