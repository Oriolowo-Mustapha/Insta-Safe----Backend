namespace InstaSafe.Domain.Enums;

public enum EscrowTransactionStatus
{
    Pending = 0,
    Funded = 1,
    Released = 2,
    Refunded = 3,
    Expired = 4,
    Failed = 5
}
