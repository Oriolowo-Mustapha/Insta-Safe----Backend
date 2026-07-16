namespace InstaSafe.Domain.Enums;

public enum DeliveryFailureReason
{
    None = 0,
    SessionMismatch = 1,
    FingerprintMismatch = 2,
    OrderMismatch = 3,
    SessionExpired = 4
}
