namespace InstaSafe.Domain.Entities;

/// <summary>
/// Phone-only driver identity. No profiles, no stored bank details —
/// drivers prove phone ownership via OTP; payout details live per-order.
/// Rows are auto-created on first OTP request.
/// </summary>
public class Dispatcher : Common.BaseEntity
{
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? OtpHash { get; set; }
    public DateTimeOffset? OtpExpiresAt { get; set; }
    public int OtpAttempts { get; set; }
}
