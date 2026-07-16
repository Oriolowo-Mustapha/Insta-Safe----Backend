using InstaSafe.Domain.Common;

namespace InstaSafe.Domain.Entities;

public class Buyer : BaseEntity
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string Phone { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
