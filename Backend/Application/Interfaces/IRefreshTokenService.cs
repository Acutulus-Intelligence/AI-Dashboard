using Domain.Models;

namespace Application.Interfaces;

public interface IRefreshTokenService
{
    Task<string> CreateRefreshTokenAsync(Guid userId, string jwtId);
    Task<RefreshToken?> ValidateRefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Returns the stored token regardless of its state (used, revoked or
    /// expired) so callers can detect reuse. Prefer
    /// <see cref="ValidateRefreshTokenAsync"/> when only an active token is valid.
    /// </summary>
    Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken);
    Task<string> RotateRefreshTokenAsync(string oldRefreshToken, string newJwtId);
    Task RevokeRefreshTokenAsync(string refreshToken);
    Task RevokeAllRefreshTokensAsync(Guid userId);
}
