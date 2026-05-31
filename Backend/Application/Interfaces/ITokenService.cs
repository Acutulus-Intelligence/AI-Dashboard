using Domain.Models;

namespace Application.Interfaces;

public interface ITokenService
{
    (string accessToken, string jwtId, int expiresIn) GenerateAccessToken(User user, IList<string> roles);
    System.Security.Claims.ClaimsPrincipal? GetPrincipalFromExpiredToken(string accessToken);
}
