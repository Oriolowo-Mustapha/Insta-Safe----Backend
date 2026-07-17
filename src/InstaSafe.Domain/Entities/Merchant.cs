using InstaSafe.Domain.Common;

namespace InstaSafe.Domain.Entities;

public class Merchant : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string BusinessName { get; set; }
    public required string Email { get; set; }
    public required string Phone { get; set; }
    public string? MonnifySubAccountCode { get; set; }
    public string? PayoutBankAccount { get; set; }
    public string? PayoutBankCode { get; set; }
    public decimal CommissionRate { get; set; } = 0.05m;
    public bool IsVerified { get; set; } = false;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
