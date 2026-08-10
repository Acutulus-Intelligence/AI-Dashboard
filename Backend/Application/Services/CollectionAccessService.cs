using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class CollectionAccessService : ICollectionAccessService
{
    private readonly IApplicationDbContext _db;

    public CollectionAccessService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DataCollection?> FindViewableAsync(Guid collectionId, Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.CompanyRole)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return null;

        var collection = await _db.DataCollections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == collectionId, ct);
        if (collection is null)
            return null;

        if (collection.CompanyId is null)
            return collection.CreatedById == userId ? collection : null;

        if (user.CompanyId != collection.CompanyId)
            return null;

        if (collection.CreatedById == userId)
            return collection;

        var company = await _db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == collection.CompanyId.Value, ct);
        if (company is not null && company.OwnerId == userId)
            return collection;

        switch (collection.Visibility)
        {
            case CollectionVisibility.Company:
                return collection;
            case CollectionVisibility.Roles:
                if (user.CompanyRoleId.HasValue && collection.AllowedRoleIds.Contains(user.CompanyRoleId.Value))
                    return collection;
                if (user.CompanyRole is not null && user.CompanyRole.CanManageConnections)
                    return collection;
                return null;
            default:
                return null;
        }
    }

    public async Task<bool> CanViewAsync(Guid collectionId, Guid userId, CancellationToken ct = default)
    {
        return await FindViewableAsync(collectionId, userId, ct) is not null;
    }

    public async Task<DataCollection?> FindManageableAsync(Guid collectionId, Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.CompanyRole)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return null;

        var collection = await _db.DataCollections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == collectionId, ct);
        if (collection is null)
            return null;

        if (collection.CreatedById == userId)
            return collection;

        if (collection.CompanyId is null)
            return null;

        if (user.CompanyId != collection.CompanyId)
            return null;

        var company = await _db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == collection.CompanyId.Value, ct);
        if (company is not null && company.OwnerId == userId)
            return collection;

        return user.CompanyRole is not null &&
               user.CompanyRole.CanManageConnections &&
               collection.Visibility != CollectionVisibility.Private
            ? collection
            : null;
    }

    public async Task<bool> CanManageAsync(Guid collectionId, Guid userId, CancellationToken ct = default)
    {
        return await FindManageableAsync(collectionId, userId, ct) is not null;
    }

    public async Task<bool> HasCollectionManagePermissionAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.CompanyRole)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return false;

        if (user.UserType != UserType.Company)
            return true;

        var company = await _db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == user.CompanyId, ct);
        if (company is not null && company.OwnerId == userId)
            return true;

        return user.CompanyRole is not null && user.CompanyRole.CanManageConnections;
    }
}