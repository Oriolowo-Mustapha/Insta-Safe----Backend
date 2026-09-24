namespace InstaSafe.Application.Common.Interfaces;

public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct);
}
