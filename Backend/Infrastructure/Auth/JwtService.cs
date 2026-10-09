using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Auth;

public class JwtService : ITokenService
{
    private readonly JwtSettings _settings;

    public JwtService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    private string TwoFactorAudience => $"{_settings.Audience}:2fa";

    public (string accessToken, string jwtId, int expiresIn) GenerateAccessToken(User user, IList<string> roles)
    {
        var jwtId = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new("userId", user.Id.ToString()),
            new("userType", ((int)user.UserType).ToString()),
            new("securityStamp", user.SecurityStamp ?? ""),
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return (accessToken, jwtId, _settings.AccessTokenExpirationMinutes * 60);
    }

    public (string challengeToken, int expiresIn) GenerateTwoFactorChallengeToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("userId", user.Id.ToString()),
            new("token_use", "2fa"),
            new("securityStamp", user.SecurityStamp ?? ""),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expires = DateTime.UtcNow.AddMinutes(_settings.TwoFactorChallengeExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: TwoFactorAudience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        var challengeToken = new JwtSecurityTokenHandler().WriteToken(token);
        return (challengeToken, _settings.TwoFactorChallengeExpirationMinutes * 60);
    }

    public (Guid UserId, string SecurityStamp)? ValidateTwoFactorChallengeToken(string challengeToken)
    {
        if (string.IsNullOrWhiteSpace(challengeToken))
            return null;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = _settings.Issuer,
            ValidAudience = TwoFactorAudience,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.Zero,
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(challengeToken, tokenValidationParameters, out _);

            if (principal.FindFirst("token_use")?.Value != "2fa")
                return null;

            var userIdClaim = principal.FindFirst("userId")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return null;

            var securityStamp = principal.FindFirst("securityStamp")?.Value ?? string.Empty;
            return (userId, securityStamp);
        }
        catch
        {
            return null;
        }
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string accessToken)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = false,
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = key,
        };

        var tokenHandler = new JwtSecurityTokenHandler();

        try
        {
            var principal = tokenHandler.ValidateToken(accessToken, tokenValidationParameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
