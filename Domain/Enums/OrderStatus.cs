namespace InstaSafe.Domain.Enums;

public enum OrderStatus
{
    Draft = 0,
    AwaitingPayment = 1,
    Held = 2,
    Delivered = 7,
    Released = 3,
    Refunded = 4,
    Disputed = 5,
    Cancelled = 6
}
