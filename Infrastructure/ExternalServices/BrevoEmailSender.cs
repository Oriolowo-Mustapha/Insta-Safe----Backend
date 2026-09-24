using InstaSafe.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace InstaSafe.Infrastructure.ExternalServices;

public class BrevoEmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _pass;
    private readonly string _from;
    private readonly string _fromName;
    private readonly ILogger<BrevoEmailSender> _logger;

    public BrevoEmailSender(IConfiguration config, ILogger<BrevoEmailSender> logger)
    {
        _host = config["Smtp:Host"] ?? "smtp-relay.brevo.com";
        _port = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
        _user = config["Smtp:Username"] ?? string.Empty;
        _pass = config["Smtp:Password"] ?? string.Empty;
        _from = config["Smtp:FromEmail"] ?? "no-reply@instasafe.ng";
        _fromName = config["Smtp:FromName"] ?? "InstaSafe";
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_user) && !string.IsNullOrWhiteSpace(_pass);

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            _logger.LogInformation("[Email:mock] To {To}: {Subject}", toEmail, subject);
            return;
        }

        using var client = new SmtpClient(_host, _port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_user, _pass),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 20000
        };
        using var message = new MailMessage
        {
            From = new MailAddress(_from, _fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);
        await client.SendMailAsync(message, ct);
    }
}
