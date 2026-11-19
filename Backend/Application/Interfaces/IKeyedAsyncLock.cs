namespace Application.Interfaces;

public interface IKeyedAsyncLock
{
    Task<IDisposable> AcquireAsync(string key, CancellationToken ct = default);
}
