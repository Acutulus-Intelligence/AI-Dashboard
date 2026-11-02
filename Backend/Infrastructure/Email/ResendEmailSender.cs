using Application.Common.Exceptions;
using Application.Interfaces;
using Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Resend;
using AppEmailMessage = Application.Interfaces.EmailMessage;

namespace Infrastructure.Email;

/// <summary>Delivers transactional email through the Resend API.</summary>
public class ResendEmailSender : IEmailSender
{
    private readonly IResend _resend;
    private readonly EmailSettings _settings;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(IResend resend, IOptions<EmailSettings> settings, ILogger<ResendEmailSender> logger)
    {
        _resend = resend;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(AppEmailMessage message, CancellationToken ct = default)
    {
        if (!_settings.IsConfigured)
            throw new EmailDeliveryException(
                "Email delivery is not configured. Set Email:ResendApiKey.",
                "email_not_configured");

        var email = new Resend.EmailMessage
        {
            From = $"{_settings.FromName} <{_settings.FromAddress}>",
            To = message.To,
            Subject = message.Subject,
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        };

        try
        {
            await _resend.EmailSendAsync(email, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient} with subject {Subject}", message.To, message.Subject);
            throw new EmailDeliveryException(
                "The invitation email could not be sent. Please try again or contact support.",
                "email_delivery_failed");
        }

        _logger.LogInformation("Sent email to {Recipient} with subject {Subject}", message.To, message.Subject);
    }
}
