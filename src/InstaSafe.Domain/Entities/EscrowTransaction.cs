using InstaSafe.Domain.Common;
using InstaSafe.Domain.Enums;

namespace InstaSafe.Domain.Entities;

public class EscrowTransaction : BaseEntity
{
    public Guid OrderId { get; init; }
    public string? MonnifyTransactionReference { get; init; }
    public string? CheckoutUrl { get; set; }
    public string? TransactionReference { get; init; }
    public PaymentChannel Channel { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "NGN";
    public EscrowTransactionStatus Status { get; private set; } = EscrowTransactionStatus.Pending;
    public string? VirtualAccountNumber { get; init; }
    public string? VirtualBankCode { get; init; }
    public DateTime? VirtualAccountExpiresAt { get; init; }
    public DateTime? FundedAt { get; private set; }
    public DateTime? ReleasedAt { get; private set; }
    public DateTime? RefundedAt { get; private set; }

    public virtual Order Order { get; private set; } = null!;

    public void MarkAsFunded(DateTime fundedAt)
    {
        Status = EscrowTransactionStatus.Funded;
        FundedAt = fundedAt;
    }

    public void MarkAsReleased(DateTime releasedAt)
    {
        Status = EscrowTransactionStatus.Released;
        ReleasedAt = releasedAt;
    }

    public void MarkAsRefunded(DateTime refundedAt)
    {
        Status = EscrowTransactionStatus.Refunded;
        RefundedAt = refundedAt;
    }

    public void Expire()
    {
        Status = EscrowTransactionStatus.Expired;
    }

    public void MarkAsFailed()
    {
        Status = EscrowTransactionStatus.Failed;
    }
}
