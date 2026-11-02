using System.Collections.Concurrent;
using Application.Common.Exceptions;
using Application.Interfaces;

namespace Presentation.IntegrationTests;

public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    /// <summary>When set, the next send throws instead of delivering, simulating a provider failure.</summary>
    public bool FailNextSend { get; set; }

    public IReadOnlyCollection<EmailMessage> Messages => _messages.ToArray();

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (FailNextSend)
        {
            FailNextSend = false;
            throw new EmailDeliveryException("Simulated email delivery failure.", "email_delivery_failed");
        }

        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyCollection<EmailMessage> MessagesTo(string recipient)
        => _messages.Where(m => string.Equals(m.To, recipient, StringComparison.OrdinalIgnoreCase)).ToArray();
}
