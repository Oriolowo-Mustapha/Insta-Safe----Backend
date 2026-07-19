using InstaSafe.Domain.Common;
using InstaSafe.Domain.Enums;

namespace InstaSafe.Domain.Entities;

public class ChatbotSession : BaseEntity
{
    public string PhoneNumber { get; private set; }
    public ChatbotState CurrentState { get; private set; }
    public DateTime LastInteractionAt { get; private set; }
    public string? TemporaryData { get; private set; } // JSON blob to store ongoing order data

    public ChatbotSession(string phoneNumber)
    {
        PhoneNumber = phoneNumber;
        CurrentState = ChatbotState.Idle;
        LastInteractionAt = DateTime.UtcNow;
    }

    public void UpdateState(ChatbotState state, string? temporaryData = null)
    {
        CurrentState = state;
        if (temporaryData != null)
        {
            TemporaryData = temporaryData;
        }
        LastInteractionAt = DateTime.UtcNow;
    }

    public void Reset()
    {
        CurrentState = ChatbotState.Idle;
        TemporaryData = null;
        LastInteractionAt = DateTime.UtcNow;
    }
}
