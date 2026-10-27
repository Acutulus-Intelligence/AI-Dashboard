using Domain.Models;

namespace Application.Interfaces;

public interface ITokenService
{
    (string accessToken, string jwtId, int expiresIn) GenerateAccessToken(User user, IList<string> roles);
    (string challengeToken, int expiresIn) GenerateTwoFactorChallengeToken(User user);
    (Guid UserId, string SecurityStamp)? ValidateTwoFactorChallengeToken(string challengeToken);
    System.Security.Claims.ClaimsPrincipal? GetPrincipalFromExpiredToken(string accessToken);
}
