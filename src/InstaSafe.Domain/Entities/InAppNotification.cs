using InstaSafe.Domain.Common;

namespace InstaSafe.Domain.Entities;

public class InAppNotification : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Title { get; private set; }
    public string Message { get; private set; }
    public bool IsRead { get; private set; }

    public InAppNotification(Guid userId, string title, string message)
    {
        UserId = userId;
        Title = title;
        Message = message;
        IsRead = false;
    }

    public void MarkAsRead()
    {
        IsRead = true;
    }
}
