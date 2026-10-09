using Application.DTos;
using Application.DTos.Request;
using Application.DTos.Response;

namespace Application.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<LoginOutcome> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResult> LoginTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken ct = default);
    Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeTokenAsync(string refreshToken, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
    Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);
    Task DeleteAccountAsync(Guid userId, DeleteAccountRequest request, CancellationToken ct = default);
    Task<UserMeResponse> GetMeAsync(Guid userId, CancellationToken ct = default);
    Task<TwoFactorSetupResponse> SetupTwoFactorAsync(Guid userId, CancellationToken ct = default);
    Task<TwoFactorRecoveryCodesResponse> EnableTwoFactorAsync(Guid userId, EnableTwoFactorRequest request, CancellationToken ct = default);
    Task DisableTwoFactorAsync(Guid userId, DisableTwoFactorRequest request, CancellationToken ct = default);
    Task<TwoFactorRecoveryCodesResponse> RegenerateRecoveryCodesAsync(Guid userId, RegenerateRecoveryCodesRequest request, CancellationToken ct = default);
}
