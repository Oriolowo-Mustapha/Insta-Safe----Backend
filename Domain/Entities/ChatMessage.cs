namespace InstaSafe.Domain.Entities;

public enum ChatDirection
{
    Inbound = 0,
    Outbound = 1
}

public class ChatMessage : Common.BaseEntity
{
    public string Phone { get; set; } = string.Empty;
    public ChatDirection Direction { get; set; }
    public string Body { get; set; } = string.Empty;
}
