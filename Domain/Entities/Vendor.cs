namespace InstaSafe.Domain.Entities;

public class Vendor : Common.BaseEntity
{
    public string Phone { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? AccountNumber { get; set; }
    public string? BankCode { get; set; }
    public string? PaystackRecipientCode { get; set; }
    public bool IsActive { get; set; } = true;
    public string? OtpHash { get; set; }
    public DateTimeOffset? OtpExpiresAt { get; set; }
    public int OtpAttempts { get; set; }
}
