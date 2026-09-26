namespace InstaSafe.Domain.Entities;

public enum DraftTicketStatus
{
    Open = 0,
    Completed = 1,
    Abandoned = 2,
    Discarded = 3
}

public class SavedOrderDraft : Common.BaseEntity
{
    public string VendorPhone { get; set; } = string.Empty;
    public string DraftJson { get; set; } = string.Empty;
    public DraftTicketStatus Status { get; set; } = DraftTicketStatus.Open;
    public Guid? CompletedOrderId { get; set; }
}
