using Domain.Models;

namespace Application.Interfaces;

public interface IRefreshTokenService
{
    Task<string> CreateRefreshTokenAsync(Guid userId, string jwtId);
    Task<RefreshToken?> ValidateRefreshTokenAsync(string refreshToken);
    Task<string> RotateRefreshTokenAsync(string oldRefreshToken, string newJwtId);
    Task RevokeRefreshTokenAsync(string refreshToken);
}
