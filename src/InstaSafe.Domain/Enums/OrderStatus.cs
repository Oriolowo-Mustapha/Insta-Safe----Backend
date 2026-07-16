namespace InstaSafe.Domain.Enums;

public enum OrderStatus
{
    Draft = 0,
    PendingPayment = 1,
    FundedInEscrow = 2,
    Dispatched = 3,
    Delivered = 4,
    Disputed = 5,
    CompletedReleased = 6,
    Refunded = 7,
    Expired = 8
}
