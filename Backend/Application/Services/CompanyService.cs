using Application.Common.Exceptions;
using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Charts;
using Domain.Enums;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class CompanyService : ICompanyService
{
    private readonly IApplicationDbContext _db;
    private readonly UserManager<User> _userManager;
    private readonly IPaymentService _paymentService;

    public CompanyService(IApplicationDbContext db, UserManager<User> userManager, IPaymentService paymentService)
    {
        _db = db;
        _userManager = userManager;
        _paymentService = paymentService;
    }

    public async Task<CompanyResponse> CreateAsync(Guid ownerId, string name, CancellationToken ct = default)
    {
        var owner = await _db.Users.FindAsync([ownerId], ct);
        if (owner is null)
            throw new KeyNotFoundException("Owner user not found.");

        if (owner.CompanyId is not null)
            throw new InvalidOperationException("You already belong to a company.");

        var nameExists = await _db.Companies.AnyAsync(c => c.Name == name, ct);
        if (nameExists)
            throw new ConflictException("A company with this name already exists.", "company_name_conflict");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerId = ownerId
        };

        owner.UserType = UserType.Company;
        owner.CompanyId = company.Id;

        _db.Companies.Add(company);

        var ownerRole = new CompanyRole
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Name = "Owner",
            IsSystemRole = true,
            CanViewAllDashboards = true,
            CanManageUsers = true,
            CanManageRoles = true,
            CanManageDashboards = true,
            CanManageConnections = true,
            AllowedTables = []
        };

        _db.CompanyRoles.Add(ownerRole);
        owner.CompanyRoleId = ownerRole.Id;

        await _db.SaveChangesAsync(ct);

        return await BuildCompanyResponseAsync(company.Id, ct);
    }

    public async Task<CompanyResponse> GetByIdAsync(Guid companyId, Guid userId, CancellationToken ct = default)
    {
        await EnsureCanViewCompanyAsync(companyId, userId, ct);
        return await BuildCompanyResponseAsync(companyId, ct);
    }

    public async Task<List<CompanyUserResponse>> GetUsersAsync(Guid companyId, Guid userId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(companyId, userId, ct);

        var company = await _db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new KeyNotFoundException("Company not found.");

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => u.CompanyId == companyId)
            .Select(u => new CompanyUserResponse(
                u.Id,
                u.Email ?? "",
                u.FirstName,
                u.LastName,
                u.CompanyRole != null ? u.CompanyRole.Name : null,
                u.CompanyRoleId,
                u.Id == company.OwnerId
            ))
            .ToListAsync(ct);

        return users;
    }

    public async Task UpdateUserRoleAsync(Guid companyId, Guid targetUserId, Guid roleId, Guid actorId, CancellationToken ct = default)
    {
        var (company, actor) = await EnsureCanManageUsersAsync(companyId, actorId, ct);

        if (targetUserId == company.OwnerId)
            throw new InvalidOperationException("Cannot change the owner's role.");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == targetUserId && u.CompanyId == companyId, ct)
            ?? throw new KeyNotFoundException("User not found in this company.");

        if (actorId != company.OwnerId && user.CompanyRoleId is not null)
        {
            var targetRole = await _db.CompanyRoles.FindAsync([user.CompanyRoleId.Value], ct);
            if (targetRole is not null &&
                targetRole.CanManageUsers == actor.CompanyRole!.CanManageUsers &&
                targetRole.CanManageRoles == actor.CompanyRole!.CanManageRoles)
                throw new UnauthorizedAccessException("Cannot change the role of a user with the same permissions as you.");
        }

        var role = await _db.CompanyRoles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.CompanyId == companyId, ct)
            ?? throw new KeyNotFoundException("Role not found in this company.");

        if (role.IsSystemRole && role.Name == "Owner")
            throw new InvalidOperationException("Cannot assign the Owner role directly. Use transfer ownership instead.");

        user.CompanyRoleId = roleId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task TransferOwnershipAsync(Guid companyId, Guid currentOwnerId, TransferOwnershipRequest request, CancellationToken ct = default)
    {
        var company = await _db.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new KeyNotFoundException("Company not found.");

        if (company.OwnerId != currentOwnerId)
            throw new UnauthorizedAccessException("Only the current owner can transfer ownership.");

        var owner = await _userManager.FindByIdAsync(currentOwnerId.ToString());
        if (owner is null)
            throw new UnauthorizedAccessException("Owner not found.");

        if (!await _userManager.CheckPasswordAsync(owner, request.CurrentPassword))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        var newOwner = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.NewOwnerId && u.CompanyId == companyId, ct)
            ?? throw new KeyNotFoundException("New owner must be a member of this company.");

        var oldOwner = await _db.Users.FindAsync([currentOwnerId], ct);

        company.OwnerId = request.NewOwnerId;

        if (oldOwner is not null)
            oldOwner.CompanyRoleId = null;

        var ownerRole = await _db.CompanyRoles
            .FirstOrDefaultAsync(r => r.CompanyId == companyId && r.Name == "Owner" && r.IsSystemRole, ct);

        if (ownerRole is not null)
            newOwner.CompanyRoleId = ownerRole.Id;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<string> InviteUserAsync(Guid companyId, string email, Guid roleId, Guid actorId, CancellationToken ct = default)
    {
        var (company, _) = await EnsureCanManageUsersAsync(companyId, actorId, ct);

        var role = await _db.CompanyRoles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.CompanyId == companyId, ct)
            ?? throw new KeyNotFoundException("Role not found in this company.");

        if (role.IsSystemRole && role.Name == "Owner")
            throw new InvalidOperationException("Cannot invite users as Owner.");

        var existingUser = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email != null && u.Email.ToLower() == email.ToLower(), ct);
        if (existingUser?.CompanyId == companyId)
            throw new ConflictException("User is already a member of this company.", "already_member");

        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        var invite = new CompanyInvite
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Email = email,
            RoleId = roleId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsAccepted = false
        };

        _db.CompanyInvites.Add(invite);
        await _db.SaveChangesAsync(ct);

        return token;
    }

    public async Task AcceptInviteAsync(string token, Guid userId, CancellationToken ct = default)
    {
        var invite = await _db.CompanyInvites
            .FirstOrDefaultAsync(i => i.Token == token && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow, ct)
            ?? throw new InvalidOperationException("Invalid or expired invite token.");

        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.CompanyId is not null && user.CompanyId != invite.CompanyId)
            throw new ConflictException("User is already a member of a different company.", "already_in_company");

        if (user.CompanyId == invite.CompanyId)
            throw new ConflictException("User is already a member of this company.", "already_member");

        user.CompanyId = invite.CompanyId;
        user.CompanyRoleId = invite.RoleId;
        user.UserType = UserType.Company;

        invite.IsAccepted = true;

        await _db.SaveChangesAsync(ct);
    }

    public async Task AcceptInviteByIdAsync(Guid inviteId, Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new KeyNotFoundException("User not found.");

        var invite = await _db.CompanyInvites
            .FirstOrDefaultAsync(i => i.Id == inviteId && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow, ct)
            ?? throw new InvalidOperationException("Invalid or expired invite.");

        if (!string.Equals(invite.Email, user.Email, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("This invite is not for you.");

        if (user.CompanyId is not null && user.CompanyId != invite.CompanyId)
            throw new ConflictException("User is already a member of a different company.", "already_in_company");

        if (user.CompanyId == invite.CompanyId)
            throw new ConflictException("User is already a member of this company.", "already_member");

        user.CompanyId = invite.CompanyId;
        user.CompanyRoleId = invite.RoleId;
        user.UserType = UserType.Company;

        invite.IsAccepted = true;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<CompanyInviteResponse>> GetInvitesAsync(Guid companyId, Guid userId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(companyId, userId, ct);

        var invites = await _db.CompanyInvites
            .AsNoTracking()
            .Include(i => i.Role)
            .Include(i => i.Company)
            .Where(i => i.CompanyId == companyId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new CompanyInviteResponse(
                i.Id,
                i.Email,
                i.Role.Name,
                i.RoleId,
                i.CreatedAt,
                i.ExpiresAt,
                i.ExpiresAt <= DateTime.UtcNow,
                i.IsAccepted,
                i.Company.Name
            ))
            .ToListAsync(ct);

        return invites;
    }

    public async Task<List<CompanyInviteResponse>> GetPendingInvitesAsync(string email, CancellationToken ct = default)
    {
        var invites = await _db.CompanyInvites
            .AsNoTracking()
            .Include(i => i.Role)
            .Include(i => i.Company)
            .Where(i => i.Email.ToLower() == email.ToLower() && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new CompanyInviteResponse(
                i.Id,
                i.Email,
                i.Role.Name,
                i.RoleId,
                i.CreatedAt,
                i.ExpiresAt,
                false,
                false,
                i.Company.Name
            ))
            .ToListAsync(ct);

        return invites;
    }

    public async Task<CompanyResponse> GetMyCompanyAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.CompanyId is null)
            throw new InvalidOperationException("You are not a member of any company.");

        return await GetByIdAsync(user.CompanyId.Value, userId, ct);
    }

    public async Task<CompanyStyleResponse> GetMyStyleAsync(Guid userId, CancellationToken ct = default)
    {
        var company = await GetMembershipCompanyAsync(userId, ct);
        return new CompanyStyleResponse(CompanyStyleSanitizer.ResolveColors(company.StyleConfig));
    }

    public async Task<CompanyStyleResponse> UpdateMyStyleAsync(
        Guid userId, UpdateCompanyStyleRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.CompanyId is null)
            throw new InvalidOperationException("You are not a member of any company.");

        var company = await _db.Companies
            .FirstOrDefaultAsync(c => c.Id == user.CompanyId.Value, ct)
            ?? throw new KeyNotFoundException("Company not found.");

        if (company.OwnerId != userId)
            throw new UnauthorizedAccessException("Only the company owner can update style settings.");

        var previousColors = CompanyStyleSanitizer.ResolveColors(company.StyleConfig);

        var sanitized = CompanyStyleSanitizer.Sanitize(new CompanyStyleConfig { Colors = request.Colors });
        if (sanitized?.Colors is null || sanitized.Colors.Count == 0)
            throw new InvalidOperationException("At least one valid colour is required.");

        var nextColors = sanitized.Colors;
        await RemapRemovedChartColorsAsync(company.Id, previousColors, nextColors, ct);

        company.StyleConfig = sanitized;
        await _db.SaveChangesAsync(ct);

        return new CompanyStyleResponse(CompanyStyleSanitizer.ResolveColors(company.StyleConfig));
    }

    /// <summary>
    /// Charts that still reference a removed company swatch fall back to the first
    /// remaining palette colour (the only colour when one is left).
    /// </summary>
    private async Task RemapRemovedChartColorsAsync(
        Guid companyId,
        List<string> previousColors,
        List<string> nextColors,
        CancellationToken ct)
    {
        var removed = previousColors
            .Where(old => !nextColors.Exists(n =>
                string.Equals(n, old, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (removed.Count == 0) return;

        var fallback = nextColors[0];
        var memberIds = _db.Users
            .Where(u => u.CompanyId == companyId)
            .Select(u => u.Id);

        var charts = await _db.SavedCharts
            .Where(sc => memberIds.Contains(sc.UserId) && sc.StyleConfig != null)
            .ToListAsync(ct);

        foreach (var chart in charts)
        {
            var style = chart.StyleConfig;
            if (style?.Colors is null || style.Colors.Count == 0) continue;

            var rewritten = false;
            var colors = style.Colors.ToList();
            for (var i = 0; i < colors.Count; i++)
            {
                var current = colors[i];
                if (string.IsNullOrWhiteSpace(current)) continue;
                if (!removed.Exists(r => string.Equals(r, current, StringComparison.OrdinalIgnoreCase)))
                    continue;

                colors[i] = fallback;
                rewritten = true;
            }

            if (!rewritten) continue;

            // Reassign so the jsonb value converter detects the change.
            chart.StyleConfig = new ChartStyleConfig
            {
                Variant = style.Variant,
                Palette = style.Palette,
                Colors = colors,
                Params = style.Params,
                ValuePrefix = style.ValuePrefix,
                ValueSuffix = style.ValueSuffix,
                Info = style.Info,
                Decimals = style.Decimals,
                DecimalMode = style.DecimalMode,
            };
            chart.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task<Company> GetMembershipCompanyAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (user.CompanyId is null)
            throw new InvalidOperationException("You are not a member of any company.");

        return await _db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == user.CompanyId.Value, ct)
            ?? throw new KeyNotFoundException("Company not found.");
    }

    public async Task DeleteAsync(Guid companyId, Guid actorId, DeleteCompanyRequest request, CancellationToken ct = default)
    {
        var company = await _db.Companies
            .Include(c => c.CompanyRoles)
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new KeyNotFoundException("Company not found.");

        if (company.OwnerId != actorId)
            throw new UnauthorizedAccessException("Only the company owner can delete the company.");

        var owner = await _userManager.FindByIdAsync(actorId.ToString());
        if (owner is null)
            throw new UnauthorizedAccessException("Owner not found.");

        if (!await _userManager.CheckPasswordAsync(owner, request.CurrentPassword))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        var companySub = await _db.CompanySubscriptions
            .FirstOrDefaultAsync(s => s.CompanyId == companyId, ct);
        if (companySub?.StripeSubscriptionId is not null)
            await _paymentService.CancelSubscriptionImmediatelyAsync(
                companySub.StripeSubscriptionId, ct);

        var users = await _db.Users
            .Where(u => u.CompanyId == companyId)
            .ToListAsync(ct);

        foreach (var user in users)
        {
            user.CompanyId = null;
            user.CompanyRoleId = null;
            user.UserType = UserType.Individual;
        }

        var invites = await _db.CompanyInvites
            .Where(i => i.CompanyId == companyId)
            .ToListAsync(ct);

        var connections = await _db.ExternalConnections
            .Where(ec => ec.CompanyId == companyId)
            .ToListAsync(ct);

        _db.CompanyInvites.RemoveRange(invites);
        _db.ExternalConnections.RemoveRange(connections);
        _db.CompanyRoles.RemoveRange(company.CompanyRoles);
        _db.Companies.Remove(company);

        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeInviteAsync(Guid companyId, Guid inviteId, Guid actorId, CancellationToken ct = default)
    {
        var (_, _) = await EnsureCanManageUsersAsync(companyId, actorId, ct);

        var invite = await _db.CompanyInvites
            .FirstOrDefaultAsync(i => i.Id == inviteId && i.CompanyId == companyId && !i.IsAccepted, ct)
            ?? throw new KeyNotFoundException("Pending invite not found.");

        _db.CompanyInvites.Remove(invite);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RejectInviteAsync(Guid inviteId, Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new KeyNotFoundException("User not found.");

        var invite = await _db.CompanyInvites
            .FirstOrDefaultAsync(i => i.Id == inviteId && !i.IsAccepted && i.ExpiresAt > DateTime.UtcNow, ct)
            ?? throw new InvalidOperationException("Pending invite not found.");

        if (!string.Equals(invite.Email, user.Email, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("This invite is not for you.");

        _db.CompanyInvites.Remove(invite);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveUserAsync(Guid companyId, Guid userIdToRemove, Guid actorId, CancellationToken ct = default)
    {
        var (company, actor) = await EnsureCanManageUsersAsync(companyId, actorId, ct);

        if (userIdToRemove == company.OwnerId)
            throw new InvalidOperationException("Cannot remove the company owner. Transfer ownership first.");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userIdToRemove && u.CompanyId == companyId, ct)
            ?? throw new KeyNotFoundException("User not found in this company.");

        if (actorId != company.OwnerId && user.CompanyRoleId is not null)
        {
            var targetRole = await _db.CompanyRoles.FindAsync([user.CompanyRoleId.Value], ct);
            if (targetRole is not null &&
                targetRole.CanManageUsers == actor.CompanyRole!.CanManageUsers &&
                targetRole.CanManageRoles == actor.CompanyRole!.CanManageRoles)
                throw new UnauthorizedAccessException("Cannot remove a user with the same permissions as you.");
        }

        user.CompanyId = null;
        user.CompanyRoleId = null;
        user.UserType = UserType.Individual;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<CompanyRoleResponse>> GetRolesAsync(Guid companyId, Guid userId, CancellationToken ct = default)
    {
        await EnsureMemberAsync(companyId, userId, ct);

        var roles = await _db.CompanyRoles
            .AsNoTracking()
            .Where(r => r.CompanyId == companyId)
            .Select(r => new CompanyRoleResponse(
                r.Id,
                r.Name,
                r.IsSystemRole,
                r.CanViewAllDashboards,
                r.CanManageUsers,
                r.CanManageRoles,
                r.CanManageDashboards,
                r.CanManageConnections,
                r.AllowedTables,
                r.Users.Count
            ))
            .ToListAsync(ct);

        return roles;
    }

    public async Task<CompanyRoleResponse> CreateRoleAsync(Guid companyId, CreateRoleRequest request, Guid actorId, CancellationToken ct = default)
    {
        var (company, _) = await EnsureCanManageRolesAsync(companyId, actorId, ct);

        if (actorId != company.OwnerId && (request.CanManageUsers || request.CanManageRoles || request.CanManageConnections))
            throw new InvalidOperationException("Only the owner can grant user management, role management, or connection management permissions.");

        var role = new CompanyRole
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = request.Name,
            IsSystemRole = false,
            CanViewAllDashboards = request.CanViewAllDashboards,
            CanManageUsers = request.CanManageUsers,
            CanManageRoles = request.CanManageRoles,
            CanManageDashboards = request.CanManageDashboards,
            CanManageConnections = request.CanManageConnections,
            AllowedTables = request.AllowedTables ?? []
        };

        _db.CompanyRoles.Add(role);
        await _db.SaveChangesAsync(ct);

        return new CompanyRoleResponse(
            role.Id,
            role.Name,
            role.IsSystemRole,
            role.CanViewAllDashboards,
            role.CanManageUsers,
            role.CanManageRoles,
            role.CanManageDashboards,
            role.CanManageConnections,
            role.AllowedTables,
            0
        );
    }

    public async Task<CompanyRoleResponse> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, Guid actorId, CancellationToken ct = default)
    {
        var role = await _db.CompanyRoles
            .Include(r => r.Company)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct)
            ?? throw new KeyNotFoundException("Role not found.");

        var (_, actor) = await EnsureCanManageRolesAsync(role.CompanyId, actorId, ct);

        if (role.IsSystemRole)
            throw new InvalidOperationException("Cannot modify system roles.");

        if (actorId != role.Company.OwnerId &&
            ((request.CanManageUsers && !role.CanManageUsers) ||
             (request.CanManageRoles && !role.CanManageRoles) ||
             (request.CanManageConnections && !role.CanManageConnections)))
            throw new InvalidOperationException("Only the owner can grant user management, role management, or connection management permissions.");

        if (actorId != role.Company.OwnerId && actor.CompanyRole is not null)
        {
            if (roleId == actor.CompanyRole.Id)
                throw new InvalidOperationException("Cannot edit your own role's permissions.");

            if (role.CanManageUsers == actor.CompanyRole.CanManageUsers &&
                role.CanManageRoles == actor.CompanyRole.CanManageRoles)
                throw new InvalidOperationException("Cannot edit a role with the same permissions as yours.");
        }

        role.Name = request.Name;

        role.CanViewAllDashboards = request.CanViewAllDashboards;
        role.CanManageUsers = request.CanManageUsers;
        role.CanManageRoles = request.CanManageRoles;
        role.CanManageDashboards = request.CanManageDashboards;
        role.CanManageConnections = request.CanManageConnections;
        role.AllowedTables = request.AllowedTables ?? [];

        await _db.SaveChangesAsync(ct);

        var userCount = await _db.Users.CountAsync(u => u.CompanyRoleId == roleId, ct);

        return new CompanyRoleResponse(
            role.Id,
            role.Name,
            role.IsSystemRole,
            role.CanViewAllDashboards,
            role.CanManageUsers,
            role.CanManageRoles,
            role.CanManageDashboards,
            role.CanManageConnections,
            role.AllowedTables,
            userCount
        );
    }

    public async Task DeleteRoleAsync(Guid roleId, Guid actorId, CancellationToken ct = default)
    {
        var role = await _db.CompanyRoles
            .Include(r => r.Company)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct)
            ?? throw new KeyNotFoundException("Role not found.");

        var (_, _) = await EnsureCanManageRolesAsync(role.CompanyId, actorId, ct);

        if (role.IsSystemRole)
            throw new InvalidOperationException("Cannot delete system roles.");

        var usersInRole = await _db.Users
            .Where(u => u.CompanyRoleId == roleId)
            .ToListAsync(ct);

        foreach (var user in usersInRole)
            user.CompanyRoleId = null;

        _db.CompanyRoles.Remove(role);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<CompanyResponse> BuildCompanyResponseAsync(Guid companyId, CancellationToken ct)
    {
        var company = await _db.Companies
            .AsNoTracking()
            .Include(c => c.Owner)
            .Include(c => c.CompanyRoles)
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new KeyNotFoundException("Company not found.");

        var userCount = await _db.Users.CountAsync(u => u.CompanyId == companyId, ct);

        var subscription = await _db.CompanySubscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.Status == SubscriptionStatus.Active, ct);

        return new CompanyResponse(
            company.Id,
            company.Name,
            company.OwnerId,
            $"{company.Owner.FirstName} {company.Owner.LastName}",
            userCount,
            company.CompanyRoles.Select(r => new CompanyRoleResponse(
                r.Id,
                r.Name,
                r.IsSystemRole,
                r.CanViewAllDashboards,
                r.CanManageUsers,
                r.CanManageRoles,
                r.CanManageDashboards,
                r.CanManageConnections,
                r.AllowedTables,
                0
            )).ToList(),
            subscription is not null
                ? new CompanySubscriptionResponse(
                    subscription.Id,
                    subscription.PlanId,
                    subscription.Plan.Name,
                    subscription.Price,
                    subscription.NextPrice,
                    subscription.NextPriceEffectiveDate,
                    subscription.BillingPeriod,
                    subscription.MaxUsers,
                    subscription.StartDate,
                    subscription.EndDate,
                    subscription.Status,
                    subscription.CancelAtPeriodEnd,
                    subscription.TrialEndDate
                )
                : null
        );
    }

    private async Task EnsureCanViewCompanyAsync(Guid companyId, Guid userId, CancellationToken ct)
    {
        var isMember = await _db.Users.AnyAsync(u => u.Id == userId && u.CompanyId == companyId, ct);
        if (!isMember)
            throw new UnauthorizedAccessException("You are not a member of this company.");
    }

    private async Task EnsureMemberAsync(Guid companyId, Guid userId, CancellationToken ct)
    {
        var isMember = await _db.Users.AnyAsync(u => u.Id == userId && u.CompanyId == companyId, ct);
        if (!isMember)
            throw new UnauthorizedAccessException("You are not a member of this company.");
    }

    private async Task<(Company company, User actor)> EnsureCanManageUsersAsync(Guid companyId, Guid actorId, CancellationToken ct)
    {
        var company = await _db.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new KeyNotFoundException("Company not found.");

        var actor = await _db.Users
            .Include(u => u.CompanyRole)
            .FirstOrDefaultAsync(u => u.Id == actorId && u.CompanyId == companyId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this company.");

        if (actor.Id != company.OwnerId && (actor.CompanyRole is null || !actor.CompanyRole.CanManageUsers))
            throw new UnauthorizedAccessException("You do not have permission to manage users.");

        return (company, actor);
    }

    private async Task<(Company company, User actor)> EnsureCanManageRolesAsync(Guid companyId, Guid actorId, CancellationToken ct)
    {
        var company = await _db.Companies
            .FirstOrDefaultAsync(c => c.Id == companyId, ct)
            ?? throw new KeyNotFoundException("Company not found.");

        var actor = await _db.Users
            .Include(u => u.CompanyRole)
            .FirstOrDefaultAsync(u => u.Id == actorId && u.CompanyId == companyId, ct)
            ?? throw new UnauthorizedAccessException("You are not a member of this company.");

        if (actor.Id != company.OwnerId && (actor.CompanyRole is null || !actor.CompanyRole.CanManageRoles))
            throw new UnauthorizedAccessException("You do not have permission to manage roles.");

        return (company, actor);
    }
}
