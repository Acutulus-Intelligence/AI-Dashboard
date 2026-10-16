using System.Globalization;
using System.Security.Cryptography;
using Application.Common.Exceptions;
using Application.DTos;
using Application.DTos.Request;
using Application.DTos.Response;
using Application.Interfaces;
using Domain.Enums;
using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Application.Services;

public class AuthService : IAuthService
{
    private const string AuthenticatorIssuer = "AI-Dashboard";
    private const int RecoveryCodeCount = 10;
    private const int MaxTwoFactorSetupAttempts = 3;
    private const int DeactivationGracePeriodDays = 14;
    private const string RecoveryCodeAlphabet = "23456789BCDFGHJKMNPQRTVWXY";
    private const string ServiceTokenProvider = "AI-Dashboard";
    private const string PendingAuthenticatorKeyToken = "PendingAuthenticatorKey";
    private const string PendingAuthenticatorAttemptsToken = "PendingAuthenticatorAttempts";
    private const string LastTimeStepToken = "LastTwoFactorTimeStep";
    private const string TwoFactorFailedAttemptsToken = "TwoFactorFailedAttempts";
    private const string TwoFactorLockoutEndToken = "TwoFactorLockoutEnd";
    private const string ReusedTwoFactorCodeMessage =
        "This code has already been used. Wait for your authenticator app to show a new code, then try again.";

    private readonly UserManager<User> _userManager;
    private readonly IUserStore<User> _userStore;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ICompanyService _companyService;
    private readonly IPaymentService _paymentService;
    private readonly IApplicationDbContext _db;
    private readonly IEncryptionService _encryption;
    private readonly ITotpService _totp;
    private readonly ICurrentPasswordVerifier _currentPasswordVerifier;
    private readonly int _maxTwoFactorAttempts;
    private readonly TimeSpan _twoFactorLockoutTimeSpan;

    public AuthService(
        UserManager<User> userManager,
        IUserStore<User> userStore,
        IPasswordHasher<User> passwordHasher,
        ITokenService tokenService,
        IRefreshTokenService refreshTokenService,
        ICompanyService companyService,
        IPaymentService paymentService,
        IApplicationDbContext db,
        IEncryptionService encryption,
        ITotpService totp,
        ICurrentPasswordVerifier currentPasswordVerifier,
        IOptions<IdentityOptions> identityOptions)
    {
        _userManager = userManager;
        _userStore = userStore;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _refreshTokenService = refreshTokenService;
        _companyService = companyService;
        _paymentService = paymentService;
        _db = db;
        _encryption = encryption;
        _totp = totp;
        _currentPasswordVerifier = currentPasswordVerifier;

        var lockout = identityOptions.Value.Lockout;
        _maxTwoFactorAttempts = lockout.MaxFailedAccessAttempts;
        _twoFactorLockoutTimeSpan = lockout.DefaultLockoutTimeSpan;
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

    public async Task<AuthResult> RegisterWithInvitationAsync(string token, AcceptEmailInvitationRequest request, CancellationToken ct = default)
    {
        var invitation = await _companyService.ValidateInvitationAsync(token, ct)
            ?? throw new InvalidOperationException("This invitation is invalid or has expired.");

        var existingUser = await _userManager.FindByEmailAsync(invitation.Email);
        if (existingUser is not null)
            throw new ConflictException("Email is already registered.", "email_conflict");

        var user = new User
        {
            UserName = invitation.Email,
            Email = invitation.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            UserType = UserType.Company
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            throw new InvalidOperationException("Registration failed. Please check your input and try again.");

        await _userManager.AddToRoleAsync(user, "User");

        await _companyService.AcceptInviteAsync(token, user.Id, ct);

        var roles = await _userManager.GetRolesAsync(user);
        return await GenerateAuthResultAsync(user, roles);
    }

    public async Task<LoginOutcome> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        if (await _userManager.IsLockedOutAsync(user))
            throw new LockedOutException();

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // A correct password clears earlier password failures. Second-factor
        // failures are tracked by a separate counter so they keep accumulating.
        await _userManager.ResetAccessFailedCountAsync(user);

        if (user.TwoFactorEnabled)
        {
            if (await IsTwoFactorLockedOutAsync(user))
                throw new LockedOutException();

            var (challengeToken, _) = _tokenService.GenerateTwoFactorChallengeToken(user);
            return LoginOutcome.TwoFactorRequired(challengeToken);
        }

        var roles = await _userManager.GetRolesAsync(user);
        return LoginOutcome.Authenticated(await GenerateAuthResultAsync(user, roles));
    }

    public async Task<AuthResult> LoginTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken ct = default)
    {
        var challenge = _tokenService.ValidateTwoFactorChallengeToken(request.ChallengeToken)
            ?? throw new UnauthorizedAccessException("Your verification session has expired. Please sign in again.");

        var user = await _userManager.FindByIdAsync(challenge.UserId.ToString())
            ?? throw new UnauthorizedAccessException("Invalid verification session.");

        if (!user.TwoFactorEnabled)
            throw new UnauthorizedAccessException("Two-factor authentication is not enabled for this account.");

        if (!string.Equals(challenge.SecurityStamp, user.SecurityStamp, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Your verification session has expired. Please sign in again.");

        if (await _userManager.IsLockedOutAsync(user))
            throw new LockedOutException();

        if (await IsTwoFactorLockedOutAsync(user))
            throw new LockedOutException();

        var code = request.Code.Trim();

        if (request.UseRecoveryCode)
        {
            if (!await RedeemRecoveryCodeAsync(user, code, ct))
            {
                await RegisterTwoFactorFailureAsync(user);
                throw new UnauthorizedAccessException("Invalid verification code.");
            }
        }
        else
        {
            var key = await GetDecryptedAuthenticatorKeyAsync(user);
            var lastTimeStep = await GetLastTimeStepAsync(user);

            long matchedTimeStep = 0;
            var result = key is null
                ? TotpValidationResult.Invalid
                : _totp.Validate(key, code, lastTimeStep, out matchedTimeStep);

            if (result != TotpValidationResult.Valid)
            {
                await RegisterTwoFactorFailureAsync(user);
                throw new UnauthorizedAccessException(
                    result == TotpValidationResult.Replay ? ReusedTwoFactorCodeMessage : "Invalid verification code.");
            }

            // Remember the accepted step so the same code cannot be replayed
            // while it is still inside its validity window.
            await SetLastTimeStepAsync(user, matchedTimeStep);
        }

        await ResetTwoFactorFailuresAsync(user);
        await _userManager.ResetAccessFailedCountAsync(user);

        // Rotating the security stamp invalidates every outstanding 2FA
        // challenge token (they carry the previous stamp), so the challenge
        // used here cannot be replayed to mint another session.
        await _userManager.UpdateSecurityStampAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        return await GenerateAuthResultAsync(user, roles);
    }

    public async Task<TwoFactorSetupResponse> SetupTwoFactorAsync(
        Guid userId, SetupTwoFactorRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedAccessException("User not found.");

        if (await _userManager.IsLockedOutAsync(user))
            throw new LockedOutException();

        // Sensitive operation: require the current password so a hijacked session
        // cannot enrol an attacker-controlled authenticator.
        if (!await _userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        // Replacing an active authenticator requires disabling 2FA first (which
        // itself requires the password plus a current code or recovery code).
        if (user.TwoFactorEnabled)
            throw new InvalidOperationException(
                "Two-factor authentication is already enabled. Disable it before setting up a new authenticator.");

        // Stage the new shared key without touching the active authenticator, so
        // an unconfirmed key can never be used to sign in. The key is encrypted
        // at rest like every other stored secret.
        var sharedKey = _userManager.GenerateNewAuthenticatorKey();
        await _userManager.SetAuthenticationTokenAsync(
            user, ServiceTokenProvider, PendingAuthenticatorKeyToken, _encryption.Encrypt(sharedKey));

        // Restarting setup grants a fresh set of verification attempts.
        await SetPendingSetupAttemptsAsync(user, 0);
        await ResetTwoFactorFailuresAsync(user);

        var account = user.Email ?? user.UserName ?? userId.ToString();
        return new TwoFactorSetupResponse(sharedKey, BuildAuthenticatorUri(account, sharedKey));
    }

    public async Task<TwoFactorRecoveryCodesResponse> EnableTwoFactorAsync(
        Guid userId, EnableTwoFactorRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedAccessException("User not found.");

        if (user.TwoFactorEnabled)
            throw new InvalidOperationException("Two-factor authentication is already enabled.");

        if (await _userManager.IsLockedOutAsync(user))
            throw new LockedOutException();

        var encryptedPendingKey = await _userManager.GetAuthenticationTokenAsync(
            user, ServiceTokenProvider, PendingAuthenticatorKeyToken);
        if (string.IsNullOrWhiteSpace(encryptedPendingKey))
            throw new InvalidOperationException("Two-factor setup has not been started. Please start the setup again.");

        var pendingKey = _encryption.Decrypt(encryptedPendingKey);

        if (_totp.Validate(pendingKey, request.Code.Trim(), long.MinValue, out _) != TotpValidationResult.Valid)
        {
            // Setup failures use their own attempt counter and never count toward
            // the account lockout, so a mistyped enrolment code cannot lock sign-in.
            var attempts = await GetPendingSetupAttemptsAsync(user) + 1;
            await SetPendingSetupAttemptsAsync(user, attempts);

            if (attempts >= MaxTwoFactorSetupAttempts)
            {
                // Abort the enrolment: discard the staged key so it can never be used.
                await _userManager.RemoveAuthenticationTokenAsync(user, ServiceTokenProvider, PendingAuthenticatorKeyToken);
                await SetPendingSetupAttemptsAsync(user, 0);
                throw new InvalidOperationException(
                    "Too many incorrect verification codes. Two-factor setup was cancelled — please start the setup again.");
            }

            throw new UnauthorizedAccessException("Invalid verification code.");
        }

        // Promote the verified staged key to the active authenticator.
        await GetAuthenticatorKeyStore().SetAuthenticatorKeyAsync(user, _encryption.Encrypt(pendingKey), ct);
        await _userManager.RemoveAuthenticationTokenAsync(user, ServiceTokenProvider, PendingAuthenticatorKeyToken);
        await _userManager.RemoveAuthenticationTokenAsync(user, ServiceTokenProvider, PendingAuthenticatorAttemptsToken);

        var enableResult = await _userManager.SetTwoFactorEnabledAsync(user, true);
        if (!enableResult.Succeeded)
            throw new InvalidOperationException("Failed to enable two-factor authentication. Please try again.");

        // Enabling rotates the security stamp, invalidating active access tokens.
        // Revoke refresh tokens too so all sessions must re-authenticate with 2FA.
        await _userManager.UpdateSecurityStampAsync(user);
        await _refreshTokenService.RevokeAllRefreshTokensAsync(user.Id);
        await ResetTwoFactorFailuresAsync(user);

        var codes = await GenerateRecoveryCodesAsync(user, ct);
        return new TwoFactorRecoveryCodesResponse(codes);
    }

    public async Task DisableTwoFactorAsync(
        Guid userId, DisableTwoFactorRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedAccessException("User not found.");

        if (await _userManager.IsLockedOutAsync(user))
            throw new LockedOutException();

        // A lost phone must still be able to turn 2FA off, so accept either a
        // current authenticator code or a single-use recovery code. Failed
        // attempts never feed the account lockout — rate limiting (and the
        // lockout gate above) already protect this authenticated endpoint.
        if (!await _userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        var factorValid = !string.IsNullOrWhiteSpace(request.RecoveryCode)
            ? await RedeemRecoveryCodeAsync(user, request.RecoveryCode.Trim(), ct)
            : await VerifyActiveTotpAsync(user, request.Code);

        if (!factorValid)
            throw new UnauthorizedAccessException("Invalid verification code.");

        var disableResult = await _userManager.SetTwoFactorEnabledAsync(user, false);
        if (!disableResult.Succeeded)
            throw new InvalidOperationException("Failed to disable two-factor authentication. Please try again.");

        await GetAuthenticatorKeyStore().SetAuthenticatorKeyAsync(user, string.Empty, ct);
        await _userManager.RemoveAuthenticationTokenAsync(user, ServiceTokenProvider, PendingAuthenticatorKeyToken);
        await _userManager.RemoveAuthenticationTokenAsync(user, ServiceTokenProvider, LastTimeStepToken);
        await ResetTwoFactorFailuresAsync(user);

        var codes = await _db.TwoFactorRecoveryCodes
            .Where(c => c.UserId == user.Id)
            .ToListAsync(ct);
        _db.TwoFactorRecoveryCodes.RemoveRange(codes);
        await _db.SaveChangesAsync(ct);

        // Invalidate every existing session for this account.
        await _userManager.UpdateSecurityStampAsync(user);
        await _refreshTokenService.RevokeAllRefreshTokensAsync(user.Id);
    }

    public async Task<TwoFactorRecoveryCodesResponse> RegenerateRecoveryCodesAsync(
        Guid userId, RegenerateRecoveryCodesRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedAccessException("User not found.");

        if (!user.TwoFactorEnabled)
            throw new InvalidOperationException("Two-factor authentication is not enabled.");

        if (await _userManager.IsLockedOutAsync(user))
            throw new LockedOutException();

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        if (!await VerifyActiveTotpAsync(user, request.Code))
            throw new UnauthorizedAccessException("Invalid verification code.");

        var codes = await GenerateRecoveryCodesAsync(user, ct);
        return new TwoFactorRecoveryCodesResponse(codes);
    }

    public async Task<AuthResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        // Look the token up regardless of state so a replayed (already used)
        // token reaches RotateRefreshTokenAsync, which revokes the family.
        var storedToken = await _refreshTokenService.GetRefreshTokenAsync(refreshToken);
        if (storedToken is null || storedToken.IsRevoked || storedToken.IsExpired)
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

        // Changing the password rotates the security stamp (invalidating access tokens);
        // revoke refresh tokens so every session must sign in again.
        await _refreshTokenService.RevokeAllRefreshTokensAsync(userId);
    }

    public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new UnauthorizedAccessException("User not found.");

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;

        var emailChanged = false;
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

            emailChanged = true;
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            throw new InvalidOperationException("Profile update failed. Please check your input and try again.");

        // Email is a sign-in identifier, so changing it invalidates every
        // existing session and forces the account to sign in again.
        if (emailChanged)
        {
            await _userManager.UpdateSecurityStampAsync(user);
            await _refreshTokenService.RevokeAllRefreshTokensAsync(userId);
        }
    }

    public async Task<UserMeResponse> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new UnauthorizedAccessException("User not found.");

        var roles = await _userManager.GetRolesAsync(user);

        string? companyRoleName = null;
        if (user.CompanyRoleId is not null)
        {
            var role = await _db.CompanyRoles.FindAsync([user.CompanyRoleId], ct);
            companyRoleName = role?.Name;
        }

        return new UserMeResponse(
            user.Id,
            user.Email ?? string.Empty,
            roles.ToList(),
            user.UserType,
            user.FirstName,
            user.LastName,
            companyRoleName,
            user.TwoFactorEnabled,
            user.DeletionScheduledAt is not null,
            user.DeletionScheduledAt
        );
    }

    public async Task DeleteAccountAsync(Guid userId, DeleteAccountRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedAccessException("User not found.");

        if (user.DeletionScheduledAt is not null)
            throw new InvalidOperationException("This account is already scheduled for deletion.");

        // Verify with lockout so this endpoint cannot be used to brute-force
        // the account password.
        var passwordCheck = await _currentPasswordVerifier.VerifyAsync(user, request.CurrentPassword);
        if (passwordCheck == CurrentPasswordCheck.LockedOut)
            throw new LockedOutException();
        if (passwordCheck != CurrentPasswordCheck.Success)
            throw new UnauthorizedAccessException("Current password is incorrect.");

        if (await _userManager.IsInRoleAsync(user, "Admin"))
            throw new InvalidOperationException(
                "You must hand over the admin role to a moderator before deleting your account.");

        var ownedCompany = await _db.Companies.FirstOrDefaultAsync(c => c.OwnerId == userId, ct);
        if (ownedCompany is not null)
            throw new InvalidOperationException(
                "You must transfer company ownership before deleting your account.");

        // A second factor is required when one is configured, so a hijacked
        // session cannot schedule deletion of the account.
        if (user.TwoFactorEnabled && !await VerifyAccountFactorAsync(user, request.Code, request.RecoveryCode, ct))
        {
            await _userManager.AccessFailedAsync(user);
            throw new UnauthorizedAccessException("Invalid verification code.");
        }

        // Soft delete: deactivate the account and schedule a permanent purge.
        // Signing in during the grace period lets the user reactivate.
        var now = DateTime.UtcNow;
        user.DeactivatedAt = now;
        user.DeletionScheduledAt = now.AddDays(DeactivationGracePeriodDays);

        // Stop auto-renewal but keep the plan until the end of the paid period.
        var userSubscription = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (userSubscription is not null && !userSubscription.CancelAtPeriodEnd)
        {
            userSubscription.CancelAtPeriodEnd = true;
            if (userSubscription.StripeSubscriptionId is not null)
                await _paymentService.CancelSubscriptionAtPeriodEndAsync(
                    userSubscription.StripeSubscriptionId, ct);
        }

        // Invalidate every existing session.
        await _userManager.UpdateSecurityStampAsync(user);
        await _refreshTokenService.RevokeAllRefreshTokensAsync(userId);

        await _db.SaveChangesAsync(ct);
    }

    public async Task ReactivateAccountAsync(
        Guid userId, ReactivateAccountRequest request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedAccessException("User not found.");

        if (user.DeletionScheduledAt is null)
            throw new InvalidOperationException("This account is not scheduled for deletion.");

        if (user.DeletionScheduledAt <= DateTime.UtcNow)
            throw new InvalidOperationException(
                "The deletion grace period has ended; this account can no longer be reactivated.");

        if (await _userManager.IsLockedOutAsync(user))
            throw new LockedOutException();

        if (!await _userManager.CheckPasswordAsync(user, request.CurrentPassword))
        {
            await _userManager.AccessFailedAsync(user);
            throw new UnauthorizedAccessException("Current password is incorrect.");
        }

        if (user.TwoFactorEnabled && !await VerifyAccountFactorAsync(user, request.Code, request.RecoveryCode, ct))
        {
            await _userManager.AccessFailedAsync(user);
            throw new UnauthorizedAccessException("Invalid verification code.");
        }

        user.DeactivatedAt = null;
        user.DeletionScheduledAt = null;

        // Undo the scheduled period-end cancellation so the plan keeps renewing.
        var userSubscription = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId, ct);
        if (userSubscription is not null && userSubscription.CancelAtPeriodEnd)
        {
            if (userSubscription.StripeSubscriptionId is not null)
                await _paymentService.ReactivateSubscriptionAsync(userSubscription.StripeSubscriptionId, ct);
            userSubscription.CancelAtPeriodEnd = false;
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<AuthResult> GenerateAuthResultAsync(User user, IList<string> roles)
    {
        var (accessToken, jwtId, expiresIn) = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(user.Id, jwtId);

        return new AuthResult(accessToken, refreshToken, expiresIn);
    }

    private async Task<string?> GetDecryptedAuthenticatorKeyAsync(User user)
    {
        var stored = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrWhiteSpace(stored))
            return null;

        // Values written before encryption was introduced are stored as plaintext.
        return _encryption.IsEncrypted(stored) ? _encryption.Decrypt(stored) : stored;
    }

    private async Task<bool> VerifyActiveTotpAsync(User user, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var key = await GetDecryptedAuthenticatorKeyAsync(user);
        if (key is null)
            return false;

        // Reject replaying a code that was already accepted within its validity
        // window (shared with the login flow's replay protection).
        var lastTimeStep = await GetLastTimeStepAsync(user);
        var result = _totp.Validate(key, code.Trim(), lastTimeStep, out var matchedTimeStep);

        if (result == TotpValidationResult.Replay)
        {
            // Still count the attempt toward lockout, like any other failed code,
            // but tell the user why it was rejected.
            await _userManager.AccessFailedAsync(user);
            throw new UnauthorizedAccessException(ReusedTwoFactorCodeMessage);
        }

        if (result != TotpValidationResult.Valid)
            return false;

        await SetLastTimeStepAsync(user, matchedTimeStep);
        return true;
    }

    private async Task<bool> VerifyAccountFactorAsync(
        User user, string? code, string? recoveryCode, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(recoveryCode))
            return await RedeemRecoveryCodeAsync(user, recoveryCode.Trim(), ct);

        return await VerifyActiveTotpAsync(user, code);
    }

    private async Task<long> GetLastTimeStepAsync(User user)
    {
        var stored = await _userManager.GetAuthenticationTokenAsync(user, ServiceTokenProvider, LastTimeStepToken);
        return long.TryParse(stored, out var value) ? value : long.MinValue;
    }

    private async Task SetLastTimeStepAsync(User user, long timeStep)
    {
        await _userManager.SetAuthenticationTokenAsync(
            user, ServiceTokenProvider, LastTimeStepToken, timeStep.ToString());
    }

    private async Task<int> GetTwoFactorFailedAttemptsAsync(User user)
    {
        var stored = await _userManager.GetAuthenticationTokenAsync(
            user, ServiceTokenProvider, TwoFactorFailedAttemptsToken);
        return int.TryParse(stored, out var value) ? value : 0;
    }

    private async Task SetTwoFactorFailedAttemptsAsync(User user, int attempts)
    {
        await _userManager.SetAuthenticationTokenAsync(
            user, ServiceTokenProvider, TwoFactorFailedAttemptsToken, attempts.ToString());
    }

    private async Task<DateTime?> GetTwoFactorLockoutEndAsync(User user)
    {
        var stored = await _userManager.GetAuthenticationTokenAsync(
            user, ServiceTokenProvider, TwoFactorLockoutEndToken);
        return DateTime.TryParse(
            stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : null;
    }

    private async Task<bool> IsTwoFactorLockedOutAsync(User user)
    {
        var lockoutEnd = await GetTwoFactorLockoutEndAsync(user);
        if (lockoutEnd is null)
            return false;

        if (lockoutEnd <= DateTime.UtcNow)
        {
            await ResetTwoFactorFailuresAsync(user);
            return false;
        }

        return true;
    }

    private async Task RegisterTwoFactorFailureAsync(User user)
    {
        var attempts = await GetTwoFactorFailedAttemptsAsync(user) + 1;
        if (attempts >= _maxTwoFactorAttempts)
        {
            // Lock the second factor. The current attempt still surfaces as an
            // invalid code; the lockout takes effect on the next attempt, which
            // mirrors Identity's own password-lockout semantics.
            await _userManager.SetAuthenticationTokenAsync(
                user, ServiceTokenProvider, TwoFactorLockoutEndToken,
                DateTime.UtcNow.Add(_twoFactorLockoutTimeSpan).ToString("o", CultureInfo.InvariantCulture));
            await SetTwoFactorFailedAttemptsAsync(user, 0);
            return;
        }

        await SetTwoFactorFailedAttemptsAsync(user, attempts);
    }

    private async Task ResetTwoFactorFailuresAsync(User user)
    {
        await _userManager.RemoveAuthenticationTokenAsync(user, ServiceTokenProvider, TwoFactorFailedAttemptsToken);
        await _userManager.RemoveAuthenticationTokenAsync(user, ServiceTokenProvider, TwoFactorLockoutEndToken);
    }

    private async Task<int> GetPendingSetupAttemptsAsync(User user)
    {
        var stored = await _userManager.GetAuthenticationTokenAsync(
            user, ServiceTokenProvider, PendingAuthenticatorAttemptsToken);
        return int.TryParse(stored, out var value) ? value : 0;
    }

    private async Task SetPendingSetupAttemptsAsync(User user, int attempts)
    {
        await _userManager.SetAuthenticationTokenAsync(
            user, ServiceTokenProvider, PendingAuthenticatorAttemptsToken, attempts.ToString());
    }

    private async Task<bool> RedeemRecoveryCodeAsync(User user, string code, CancellationToken ct)
    {
        var normalized = NormalizeRecoveryCode(code);
        if (normalized.Length == 0)
            return false;

        var candidates = await _db.TwoFactorRecoveryCodes
            .AsNoTracking()
            .Where(c => c.UserId == user.Id && c.UsedAt == null)
            .ToListAsync(ct);

        foreach (var candidate in candidates)
        {
            var result = _passwordHasher.VerifyHashedPassword(user, candidate.CodeHash, normalized);
            if (result == PasswordVerificationResult.Failed)
                continue;

            // Atomically consume the code so it cannot be redeemed twice concurrently.
            var now = DateTime.UtcNow;
            var affected = await _db.TwoFactorRecoveryCodes
                .Where(c => c.Id == candidate.Id && c.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, now), ct);

            return affected == 1;
        }

        return false;
    }

    private async Task<IReadOnlyList<string>> GenerateRecoveryCodesAsync(User user, CancellationToken ct)
    {
        var existing = await _db.TwoFactorRecoveryCodes
            .Where(c => c.UserId == user.Id)
            .ToListAsync(ct);
        _db.TwoFactorRecoveryCodes.RemoveRange(existing);

        var displayCodes = new List<string>(RecoveryCodeCount);
        var now = DateTime.UtcNow;

        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var code = CreateRecoveryCode();
            displayCodes.Add(code);

            _db.TwoFactorRecoveryCodes.Add(new TwoFactorRecoveryCode
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CodeHash = _passwordHasher.HashPassword(user, NormalizeRecoveryCode(code)),
                CreatedAt = now,
            });
        }

        await _db.SaveChangesAsync(ct);
        return displayCodes;
    }

    private IUserAuthenticatorKeyStore<User> GetAuthenticatorKeyStore()
    {
        if (_userStore is IUserAuthenticatorKeyStore<User> keyStore)
            return keyStore;

        throw new InvalidOperationException("The configured user store does not support authenticator keys.");
    }

    private static string BuildAuthenticatorUri(string account, string sharedKey)
    {
        var label = Uri.EscapeDataString($"{AuthenticatorIssuer}:{account}");
        var issuer = Uri.EscapeDataString(AuthenticatorIssuer);
        return $"otpauth://totp/{label}?secret={sharedKey}&issuer={issuer}&digits=6";
    }

    private static string CreateRecoveryCode()
    {
        var chars = new char[10];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = RecoveryCodeAlphabet[RandomNumberGenerator.GetInt32(RecoveryCodeAlphabet.Length)];

        return $"{new string(chars, 0, 5)}-{new string(chars, 5, 5)}";
    }

    private static string NormalizeRecoveryCode(string code) =>
        code.Replace("-", string.Empty).Replace(" ", string.Empty).Trim().ToUpperInvariant();
}
