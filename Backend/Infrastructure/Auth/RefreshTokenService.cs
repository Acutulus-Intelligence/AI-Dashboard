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
            .FirstOrDefaultAsync(rt => rt.Token == hashed && rt.IsActive);
    }

    public async Task<string> RotateRefreshTokenAsync(string oldRefreshToken, string newJwtId)
    {
        var hashed = HashToken(oldRefreshToken);

        var storedToken = await _db.Set<RefreshToken>()
            .FirstOrDefaultAsync(rt => rt.Token == hashed);

        if (storedToken is null || !storedToken.IsActive)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        if (storedToken.IsUsed)
        {
            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.UtcNow;

            await _db.Set<RefreshToken>()
                .Where(rt => rt.UserId == storedToken.UserId && rt.JwtId == storedToken.JwtId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(rt => rt.IsRevoked, true)
                    .SetProperty(rt => rt.RevokedAt, DateTime.UtcNow));

            await _db.SaveChangesAsync();
            throw new UnauthorizedAccessException("Refresh token reuse detected. All tokens for this session have been revoked.");
        }

        storedToken.IsUsed = true;

        var newToken = GenerateToken();
        var newRefreshToken = new RefreshToken
        {
            UserId = storedToken.UserId,
            Token = HashToken(newToken),
            JwtId = newJwtId,
            ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow,
            IsUsed = false,
            IsRevoked = false,
            ReplacedByToken = HashToken(newToken),
        };

        storedToken.ReplacedByToken = HashToken(newToken);
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
