using System.Collections.Concurrent;
using Application.Interfaces;

namespace Application.Caching;

public sealed class KeyedAsyncLock : IKeyedAsyncLock
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public async Task<IDisposable> AcquireAsync(string key, CancellationToken ct = default)
    {
        Entry entry;

        while (true)
        {
            entry = _entries.GetOrAdd(key, _ => new Entry());

            lock (entry)
            {
                if (entry.Removed)
                    continue;

                entry.RefCount++;
                break;
            }
        }

        try
        {
            await entry.Semaphore.WaitAsync(ct);
        }
        catch
        {
            ReleaseReference(key, entry);
            throw;
        }

        return new Lease(this, key, entry);
    }

    private void Release(string key, Entry entry)
    {
        entry.Semaphore.Release();
        ReleaseReference(key, entry);
    }

    private void ReleaseReference(string key, Entry entry)
    {
        lock (entry)
        {
            entry.RefCount--;
            if (entry.RefCount == 0)
            {
                entry.Removed = true;
                _entries.TryRemove(new KeyValuePair<string, Entry>(key, entry));
            }
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int RefCount { get; set; }
        public bool Removed { get; set; }
    }

    private sealed class Lease : IDisposable
    {
        private KeyedAsyncLock? _owner;
        private readonly string _key;
        private readonly Entry _entry;

        public Lease(KeyedAsyncLock owner, string key, Entry entry)
        {
            _owner = owner;
            _key = key;
            _entry = entry;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Release(_key, _entry);
        }
    }
}
