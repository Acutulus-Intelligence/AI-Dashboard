namespace Application.Interfaces;

public interface IAccountDeletionService
{
    Task<int> PurgeExpiredAccountsAsync(CancellationToken ct = default);

    Task PurgeAccountAsync(Guid userId, CancellationToken ct = default);
}
