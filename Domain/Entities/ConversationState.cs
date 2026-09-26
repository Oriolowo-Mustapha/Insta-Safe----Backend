namespace InstaSafe.Domain.Entities;

public enum ConversationStep
{
    Idle = 0,
    AwaitingMenuChoice = 1,
    DraftCustomerName = 2,
    DraftCustomerPhone = 3,
    DraftBuyerEmail = 15,
    DraftAddress = 4,
    DraftItems = 5,
    DraftAmount = 6,
    Confirming = 7,
    AwaitingTrackRef = 8,
    DraftDeliveryFee = 9,
    DraftDriverPhone = 10,
    DraftDriverAccount = 11,
    DraftDriverBankName = 12,
    DraftDriverConfirm = 13,
    BrowsingDrafts = 14
}

public class ConversationState : Common.BaseEntity
{
    public string Phone { get; set; } = string.Empty;
    public ConversationStep Step { get; set; } = ConversationStep.Idle;
    public string? DraftJson { get; set; }
    public Guid? CurrentDraftId { get; set; }
}
