using Application.Common.Exceptions;
using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class AdminUserService : IAdminUserService
{
    private const string AdminRole = "Admin";
    private const string ModeratorRole = "Moderator";
    private const string UserRole = "User";
    private const int DefaultTake = 100;

    private readonly IApplicationDbContext _db;
    private readonly UserManager<User> _userManager;

    public AdminUserService(IApplicationDbContext db, UserManager<User> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<List<AdminUserResponse>> GetUsersAsync(string? search = null, int? take = null, CancellationToken ct = default)
    {
        IQueryable<User> query = _db.Users.AsNoTracking().OrderBy(u => u.Email);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                (u.Email != null && u.Email.Contains(term)) ||
                (u.FirstName != null && u.FirstName.Contains(term)) ||
                (u.LastName != null && u.LastName.Contains(term)));
        }

        var users = await query.Take(take ?? DefaultTake).ToListAsync(ct);

        var result = new List<AdminUserResponse>(users.Count);
        foreach (var user in users)
        {
            result.Add(await ToResponseAsync(user, ct));
        }

        return result;
    }

    public async Task<AdminUserResponse> SetAdminRoleAsync(Guid actorId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        if (!isAdmin)
            throw new InvalidOperationException("Admins cannot be removed by other admins. Hand over the role to a moderator instead.");

        if (actorId == userId)
            throw new InvalidOperationException("You cannot change your own admin role.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        var isCurrentlyAdmin = await _userManager.IsInRoleAsync(user, AdminRole);
        if (isAdmin && !isCurrentlyAdmin)
        {
            var addResult = await _userManager.AddToRoleAsync(user, AdminRole);
            if (!addResult.Succeeded)
                throw new InvalidOperationException("Failed to grant the admin role.");
        }

        return await ToResponseAsync(user, ct);
    }

    public async Task<AdminUserResponse> SetModeratorRoleAsync(Guid actorId, Guid userId, bool isModerator, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        if (isModerator)
        {
            var isCurrentlyAdmin = await _userManager.IsInRoleAsync(user, AdminRole);
            if (isCurrentlyAdmin)
                throw new InvalidOperationException("A user with the admin role cannot also be a moderator.");

            var addResult = await _userManager.AddToRoleAsync(user, ModeratorRole);
            if (!addResult.Succeeded)
                throw new InvalidOperationException("Failed to grant the moderator role.");
        }
        else
        {
            var isCurrentlyAdmin = await _userManager.IsInRoleAsync(user, AdminRole);
            if (isCurrentlyAdmin)
                throw new InvalidOperationException("Admins cannot be removed by other admins. Hand over the role to a moderator instead.");

            var removeResult = await _userManager.RemoveFromRoleAsync(user, ModeratorRole);
            if (!removeResult.Succeeded)
                throw new InvalidOperationException("Failed to revoke the moderator role.");
        }

        return await ToResponseAsync(user, ct);
    }

    public async Task<AdminUserResponse> TransferAdminRoleAsync(Guid actorId, Guid targetUserId, CancellationToken ct = default)
    {
        if (actorId == targetUserId)
            throw new InvalidOperationException("You cannot hand the admin role to yourself.");

        var actor = await _db.Users.FirstOrDefaultAsync(u => u.Id == actorId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        var target = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, ct)
            ?? throw new KeyNotFoundException("Target user not found.");

        if (!await _userManager.IsInRoleAsync(actor, AdminRole))
            throw new InvalidOperationException("Only admins can hand over the admin role.");

        if (!await _userManager.IsInRoleAsync(target, ModeratorRole))
            throw new InvalidOperationException("The admin role can only be handed to a moderator.");

        if (!(await _userManager.RemoveFromRoleAsync(actor, AdminRole)).Succeeded ||
            !(await _userManager.AddToRoleAsync(actor, ModeratorRole)).Succeeded)
            throw new InvalidOperationException("Failed to downgrade the current admin.");

        if (!(await _userManager.RemoveFromRoleAsync(target, ModeratorRole)).Succeeded ||
            !(await _userManager.AddToRoleAsync(target, AdminRole)).Succeeded)
            throw new InvalidOperationException("Failed to promote the moderator.");

        return await ToResponseAsync(target, ct);
    }

    public async Task<AdminUserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var role = request.Role ?? UserRole;
        if (role is not (AdminRole or ModeratorRole or UserRole))
            throw new ArgumentException("Role must be one of: User, Moderator, Admin.");

        if (role != UserRole && request.UserType == UserType.Company)
            throw new InvalidOperationException("Staff accounts (admin or moderator) must be individual users.");

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
            throw new ConflictException("Email is already registered.", "email_conflict");

        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserType = request.UserType
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            throw new InvalidOperationException("Registration failed. Please check your input and try again.");

        await _userManager.AddToRoleAsync(user, UserRole);

        if (role == ModeratorRole)
        {
            var roleResult = await _userManager.AddToRoleAsync(user, ModeratorRole);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException("Failed to grant the moderator role.");
        }
        else if (role == AdminRole)
        {
            var roleResult = await _userManager.AddToRoleAsync(user, AdminRole);
            if (!roleResult.Succeeded)
                throw new InvalidOperationException("Failed to grant the admin role.");
        }

        return await ToResponseAsync(user, ct);
    }

    public async Task<AdminStatsResponse> GetStatsAsync(CancellationToken ct = default)
    {
        var totalUsers = await _db.Users.CountAsync(ct);

        var individualIds = await _db.UserSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync(ct);

        var companyIds = await _db.CompanySubscriptions
            .Where(s => s.Status == SubscriptionStatus.Trial || s.Status == SubscriptionStatus.Active)
            .Select(s => s.CompanyId)
            .Distinct()
            .ToListAsync(ct);

        var companyUserIds = await _db.Users
            .Where(u => u.CompanyId != null && companyIds.Contains(u.CompanyId.Value))
            .Select(u => u.Id)
            .Distinct()
            .ToListAsync(ct);

        var subscribed = individualIds
            .Concat(companyUserIds)
            .Distinct()
            .Count();

        return new AdminStatsResponse(
            totalUsers,
            individualIds.Count,
            companyUserIds.Count,
            Math.Max(0, totalUsers - subscribed));
    }

    private async Task<AdminUserResponse> ToResponseAsync(User user, CancellationToken ct = default)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new AdminUserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.UserType,
            roles.Contains(AdminRole),
            roles.Contains(ModeratorRole),
            roles.ToList());
    }
}
