using Application.DTos.Request;
using Application.DTos.Response;

namespace Application.Interfaces;

public interface IChartFolderService
{
    Task<List<ChartFolderResponse>> GetFoldersAsync(Guid userId, CancellationToken ct = default);
    Task<ChartFolderResponse> CreateAsync(Guid userId, CreateChartFolderRequest request, CancellationToken ct = default);
    Task<ChartFolderResponse> RenameAsync(Guid id, Guid userId, UpdateChartFolderRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
}
