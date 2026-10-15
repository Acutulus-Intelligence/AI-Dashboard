using System.Security.Cryptography;
using System.Text;
using Application.Interfaces;
using Domain.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Auth;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _db;
    private readonly JwtSettings _settings;

    public RefreshTokenService(AppDbContext db, IOptions<JwtSettings> settings)
    {
        _db = db;
        _settings = settings.Value;
    }

    public async Task<string> CreateRefreshTokenAsync(Guid userId, string jwtId)
    {
        var token = GenerateToken();

        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = HashToken(token),
            JwtId = jwtId,
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow,
            IsUsed = false,
            IsRevoked = false,
        };

        _db.Set<RefreshToken>().Add(refreshToken);
        await _db.SaveChangesAsync();

        return token;
    }

    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string refreshToken)
    {
        var hashed = HashToken(refreshToken);

        return await _db.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.Token == hashed && !rt.IsUsed && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow);
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken)
    {
        var hashed = HashToken(refreshToken);

        return await _db.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.Token == hashed);
    }

    public async Task<string> RotateRefreshTokenAsync(string oldRefreshToken, string newJwtId)
    {
        var hashed = HashToken(oldRefreshToken);
        var now = DateTime.UtcNow;

        // Atomically claim the token so two concurrent refreshes presenting the
        // same token cannot both succeed. Only an unused, unrevoked, unexpired
        // row is claimed; a 0-row result means invalid or already used.
        var claimed = await _db.Set<RefreshToken>()
            .Where(rt => rt.Token == hashed && !rt.IsUsed && !rt.IsRevoked && rt.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(rt => rt.IsUsed, true));

        if (claimed == 0)
        {
            var existing = await _db.Set<RefreshToken>()
                .AsNoTracking()
                .FirstOrDefaultAsync(rt => rt.Token == hashed);

            if (existing is not null && existing.IsUsed && !existing.IsRevoked)
            {
                // A previously used token is being replayed: treat the account
                // as compromised and revoke every active token so all sessions
                // must sign in again. (JwtId changes on every rotation, so it
                // cannot scope this to a single token chain.)
                await _db.Set<RefreshToken>()
                    .Where(rt => rt.UserId == existing.UserId && !rt.IsRevoked)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(rt => rt.IsRevoked, true)
                        .SetProperty(rt => rt.RevokedAt, now));

                throw new UnauthorizedAccessException(
                    "Refresh token reuse detected. All sessions have been revoked.");
            }

            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        var storedToken = await _db.Set<RefreshToken>()
            .AsNoTracking()
            .FirstAsync(rt => rt.Token == hashed);

        var newToken = GenerateToken();
        var newTokenHash = HashToken(newToken);

        // The replaced token points at its successor; the successor is only
        // linked once it is itself rotated.
        await _db.Set<RefreshToken>()
            .Where(rt => rt.Token == hashed)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(rt => rt.ReplacedByToken, newTokenHash));

        var newRefreshToken = new RefreshToken
        {
            UserId = storedToken.UserId,
            Token = newTokenHash,
            JwtId = newJwtId,
            ExpiresAt = now.AddDays(_settings.RefreshTokenExpirationDays),
            CreatedAt = now,
            IsUsed = false,
            IsRevoked = false,
        };

        _db.Set<RefreshToken>().Add(newRefreshToken);
        await _db.SaveChangesAsync();

        return newToken;
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        var hashed = HashToken(refreshToken);

        var storedToken = await _db.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.Token == hashed);

        if (storedToken is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task RevokeAllRefreshTokensAsync(Guid userId)
    {
        var now = DateTime.UtcNow;

        await _db.Set<RefreshToken>()
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(rt => rt.IsRevoked, true)
                .SetProperty(rt => rt.RevokedAt, now));

        await _db.SaveChangesAsync();
    }

    private static string GenerateToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
