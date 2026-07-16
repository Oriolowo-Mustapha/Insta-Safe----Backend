namespace InstaSafe.Application.Common.Interfaces;

public interface IInAppNotificationService
{
    Task SendNotificationAsync(Guid userId, string title, string message, CancellationToken ct = default);
}
