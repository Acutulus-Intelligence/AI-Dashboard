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
            throw new InvalidOperationException("Registration failed. Please check your input and try again.");

        await _userManager.AddToRoleAsync(user, "User");

        if (!string.IsNullOrEmpty(request.InviteToken))
        {
            await _companyService.AcceptInviteAsync(request.InviteToken, user.Id, ct);
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
            throw new InvalidOperationException("Password change failed. Please check your input and try again.");
    }

    public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new UnauthorizedAccessException("User not found.");

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;

        if (request.Email is not null && !string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailOwner = await _userManager.FindByEmailAsync(request.Email);
            if (emailOwner is not null && emailOwner.Id != userId)
                throw new ConflictException("Email is already in use.", "email_conflict");

            var setEmailResult = await _userManager.SetEmailAsync(user, request.Email);
            if (!setEmailResult.Succeeded)
                throw new InvalidOperationException("Email update failed. Please check your input and try again.");

            var setUserNameResult = await _userManager.SetUserNameAsync(user, request.Email);
            if (!setUserNameResult.Succeeded)
                throw new InvalidOperationException("Profile update failed. Please check your input and try again.");
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            throw new InvalidOperationException("Profile update failed. Please check your input and try again.");
    }

    private async Task<AuthResult> GenerateAuthResultAsync(User user, IList<string> roles)
    {
        var (accessToken, jwtId, expiresIn) = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(user.Id, jwtId);

        return new AuthResult(accessToken, refreshToken, expiresIn);
    }
}
