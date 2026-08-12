using Application.DTos.Response;
using Application.Interfaces;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public class AdminUserService : IAdminUserService
{
    private const string AdminRole = "Admin";
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
        if (actorId == userId && !isAdmin)
            throw new InvalidOperationException("You cannot remove your own admin role.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new KeyNotFoundException("User not found.");

        var isCurrentlyAdmin = await _userManager.IsInRoleAsync(user, AdminRole);

        if (isAdmin && !isCurrentlyAdmin)
        {
            var addResult = await _userManager.AddToRoleAsync(user, AdminRole);
            if (!addResult.Succeeded)
                throw new InvalidOperationException("Failed to grant the admin role.");
        }
        else if (!isAdmin && isCurrentlyAdmin)
        {
            var removeResult = await _userManager.RemoveFromRoleAsync(user, AdminRole);
            if (!removeResult.Succeeded)
                throw new InvalidOperationException("Failed to revoke the admin role.");
        }

        return await ToResponseAsync(user, ct);
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
            roles.ToList());
    }
}
