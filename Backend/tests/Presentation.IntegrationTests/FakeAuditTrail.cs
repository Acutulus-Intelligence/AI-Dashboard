using System.Collections.Concurrent;
using Application.Interfaces;

namespace Presentation.IntegrationTests;

public sealed class FakeAuditTrail : IAuditTrail
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();

    public IReadOnlyList<AuditEntry> Entries => _entries.ToArray();

    public void Record(AuditEntry entry) => _entries.Enqueue(entry);

    public void Clear()
    {
        while (_entries.TryDequeue(out _))
        {
        }
    }
}
