namespace InstaSafe.Application.Common.Interfaces;

public interface IWhatsAppMessagingService
{
    Task SendMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default);
    Task SendImageAsync(string toPhoneNumber, string imageUrl, string caption, CancellationToken cancellationToken = default);
}
