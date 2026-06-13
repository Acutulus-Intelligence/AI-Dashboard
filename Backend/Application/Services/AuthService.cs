using Application.Common.Exceptions;
using Application.Dtos;
using Application.Dtos.Request;
using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.AspNetCore.Identity;

namespace Application.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ICompanyService _companyService;

    public AuthService(
        UserManager<User> userManager,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        ICompanyService companyService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _companyService = companyService;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
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
            throw new InvalidOperationException(
                $"User creation failed: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");

        await _userManager.AddToRoleAsync(user, "User");

        if (request.UserType == UserType.Company)
        {
            if (!string.IsNullOrEmpty(request.CompanyName))
            {
                await _companyService.CreateAsync(user.Id, request.CompanyName, ct);
            }
            else if (!string.IsNullOrEmpty(request.InviteToken))
            {
                await _companyService.AcceptInviteAsync(request.InviteToken, user.Id, ct);
            }
        }

        var roles = await _userManager.GetRolesAsync(user);
        return await GenerateAuthResultAsync(user, roles);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
            throw new UnauthorizedAccessException("Invalid email or password.");

        var roles = await _userManager.GetRolesAsync(user);
        return await GenerateAuthResultAsync(user, roles);
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var storedToken = await _refreshTokenService.ValidateRefreshTokenAsync(refreshToken);
        if (storedToken is null)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        var user = await _userManager.FindByIdAsync(storedToken.UserId.ToString());
        if (user is null)
            throw new UnauthorizedAccessException("User not found.");

        var roles = await _userManager.GetRolesAsync(user);
        var (newAccessToken, jwtId, expiresIn) = _tokenService.GenerateAccessToken(user, roles);

        var newRefreshToken = await _refreshTokenService.RotateRefreshTokenAsync(refreshToken, jwtId);

        return new AuthResult(newAccessToken, newRefreshToken, expiresIn);
    }

    public async Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var storedToken = await _refreshTokenService.ValidateRefreshTokenAsync(refreshToken);
        if (storedToken is null)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        await _refreshTokenService.RevokeRefreshTokenAsync(refreshToken);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new UnauthorizedAccessException("User not found.");

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Password change failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }

    public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new UnauthorizedAccessException("User not found.");

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;

        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailOwner = await _userManager.FindByEmailAsync(request.Email);
            if (emailOwner is not null && emailOwner.Id != userId)
                throw new ConflictException("Email is already in use.", "email_conflict");

            var setEmailResult = await _userManager.SetEmailAsync(user, request.Email);
            if (!setEmailResult.Succeeded)
                throw new InvalidOperationException(
                    $"Email update failed: {string.Join(", ", setEmailResult.Errors.Select(e => e.Description))}");

            var setUserNameResult = await _userManager.SetUserNameAsync(user, request.Email);
            if (!setUserNameResult.Succeeded)
                throw new InvalidOperationException(
                    $"Username update failed: {string.Join(", ", setUserNameResult.Errors.Select(e => e.Description))}");
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            throw new InvalidOperationException(
                $"Profile update failed: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
    }

    private async Task<AuthResult> GenerateAuthResultAsync(User user, IList<string> roles)
    {
        var (accessToken, jwtId, expiresIn) = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(user.Id, jwtId);

        return new AuthResult(accessToken, refreshToken, expiresIn);
    }
}
