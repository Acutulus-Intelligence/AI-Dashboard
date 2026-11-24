using Application.Interfaces;
using FluentAssertions;
using Infrastructure.Audit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Tests;

public sealed class AuditTrailTests
{
    [Fact]
    public void Audit_entry_is_redacted_before_logging()
    {
        var factory = new CapturingLoggerFactory();
        var audit = new AuditTrail(factory);

        audit.Record(new AuditEntry(
            AuditAction.ChartGenerated,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            "bar",
            "SELECT * FROM orders WHERE note = 'customer secret'",
            5,
            "success"));

        factory.Messages.Should().ContainSingle();
        var message = factory.Messages[0];
        message.Should().StartWith("Audit|Information|");
        message.Should().Contain("Audit ChartGenerated");
        message.Should().Contain("SqlHash=");
        message.Should().NotContain("customer secret");
    }

    [Fact]
    public void Cache_hits_are_logged_at_debug()
    {
        var factory = new CapturingLoggerFactory();
        var audit = new AuditTrail(factory);

        audit.Record(new AuditEntry(
            AuditAction.ChartExecuted,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            "bar",
            "SELECT * FROM orders",
            5,
            "success",
            "cache"));

        factory.Messages.Should().ContainSingle();
        factory.Messages[0].Should().StartWith("Audit|Debug|");
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages, categoryName);

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(
        List<string> messages,
        string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            messages.Add($"{category}|{logLevel}|{formatter(state, exception)}");
        }
    }
}
