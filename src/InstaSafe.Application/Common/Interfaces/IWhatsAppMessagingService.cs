namespace InstaSafe.Application.Common.Interfaces;

public interface IWhatsAppMessagingService
{
    Task SendMessageAsync(string toPhoneNumber, string message, CancellationToken cancellationToken = default);
}
