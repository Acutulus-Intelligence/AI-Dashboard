using Application.Common.Exceptions;
using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class ChartFolderService : IChartFolderService
{
    private readonly IApplicationDbContext _db;

    public ChartFolderService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<ChartFolderResponse>> GetFoldersAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.ChartFolders
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.Name)
            .Select(f => new ChartFolderResponse(
                f.Id,
                f.Name,
                _db.SavedCharts.Count(c => c.FolderId == f.Id && c.UserId == userId),
                f.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<ChartFolderResponse> CreateAsync(
        Guid userId, CreateChartFolderRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        await EnsureUniqueNameAsync(userId, name, excludeId: null, ct);

        var folder = new ChartFolder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            CreatedAt = DateTime.UtcNow,
        };

        _db.ChartFolders.Add(folder);
        await _db.SaveChangesAsync(ct);

        return new ChartFolderResponse(folder.Id, folder.Name, 0, folder.CreatedAt);
    }

    public async Task<ChartFolderResponse> RenameAsync(
        Guid id, Guid userId, UpdateChartFolderRequest request, CancellationToken ct = default)
    {
        var folder = await _db.ChartFolders
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Folder not found.");

        var name = request.Name.Trim();
        await EnsureUniqueNameAsync(userId, name, excludeId: id, ct);

        folder.Name = name;
        await _db.SaveChangesAsync(ct);

        var chartCount = await _db.SavedCharts.CountAsync(
            c => c.FolderId == id && c.UserId == userId, ct);

        return new ChartFolderResponse(folder.Id, folder.Name, chartCount, folder.CreatedAt);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var folder = await _db.ChartFolders
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Folder not found.");

        // Charts are kept; they simply become unfiled.
        await _db.SavedCharts
            .Where(c => c.FolderId == id && c.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.FolderId, (Guid?)null), ct);

        _db.ChartFolders.Remove(folder);
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureUniqueNameAsync(
        Guid userId, string name, Guid? excludeId, CancellationToken ct)
    {
        var taken = await _db.ChartFolders.AnyAsync(
            f => f.UserId == userId
                && f.Name == name
                && (!excludeId.HasValue || f.Id != excludeId.Value),
            ct);

        if (taken)
        {
            throw new ConflictException(
                $"A folder named \"{name}\" already exists. Choose another name.",
                "chart_folder_name_conflict");
        }
    }
}
