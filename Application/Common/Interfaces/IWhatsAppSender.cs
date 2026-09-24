namespace InstaSafe.Application.Common.Interfaces;

public interface IWhatsAppSender
{
    Task SendTextAsync(string toPhone, string body, CancellationToken ct);
    Task SendTemplateAsync(string toPhone, string templateName, Dictionary<string, string> vars, CancellationToken ct);
}
