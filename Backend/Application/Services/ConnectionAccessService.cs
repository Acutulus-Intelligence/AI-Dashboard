using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class ConnectionAccessService : IConnectionAccessService
{
    private readonly IApplicationDbContext _db;

    public ConnectionAccessService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ExternalConnection?> FindViewableAsync(Guid connectionId, Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.CompanyRole)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return null;

        var connection = await _db.ExternalConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(ec => ec.Id == connectionId, ct);
        if (connection is null)
            return null;

        if (connection.CompanyId is null)
            return connection.UserId == userId ? connection : null;

        if (user.CompanyId != connection.CompanyId)
            return null;

        if (connection.UserId == userId)
            return connection;

        var company = await _db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == connection.CompanyId.Value, ct);
        if (company is not null && company.OwnerId == userId)
            return connection;

        switch (connection.Visibility)
        {
            case ConnectionVisibility.Company:
                return connection;
            case ConnectionVisibility.Roles:
                if (user.CompanyRoleId.HasValue && connection.AllowedRoleIds.Contains(user.CompanyRoleId.Value))
                    return connection;
                if (user.CompanyRole is not null && user.CompanyRole.CanManageConnections)
                    return connection;
                return null;
            default:
                return null;
        }
    }

    public async Task<bool> CanViewAsync(Guid connectionId, Guid userId, CancellationToken ct = default)
    {
        return await FindViewableAsync(connectionId, userId, ct) is not null;
    }

    public async Task<bool> CanManageAsync(Guid connectionId, Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.CompanyRole)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return false;

        var connection = await _db.ExternalConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(ec => ec.Id == connectionId, ct);
        if (connection is null)
            return false;

        if (connection.UserId == userId)
            return true;

        if (connection.CompanyId is null)
            return false;

        var company = await _db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == connection.CompanyId.Value, ct);
        if (company is not null && company.OwnerId == userId)
            return true;

        return user.CompanyRole is not null &&
               user.CompanyRole.CanManageConnections &&
               connection.Visibility != ConnectionVisibility.Private;
    }

    public async Task<bool> HasConnectionManagePermissionAsync(Guid userId, CancellationToken ct = default)
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
