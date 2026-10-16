namespace Application.Interfaces;

/// <summary>
/// Outcome of validating a time-based one-time password.
/// </summary>
public enum TotpValidationResult
{
    /// <summary>The code does not match any acceptable time step.</summary>
    Invalid,

    /// <summary>The code matched a fresh time step.</summary>
    Valid,

    /// <summary>
    /// The code is correct for its time step but that step was already accepted,
    /// so it is being reused.
    /// </summary>
    Replay,
}

/// <summary>
/// Validates RFC 6238 time-based one-time passwords (TOTP) and reports which
/// time step a code matched, so callers can reject replays.
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Validates <paramref name="code"/> against <paramref name="base32Secret"/>.
    /// Codes whose time step is less than or equal to <paramref name="minTimeStep"/>
    /// are reported as <see cref="TotpValidationResult.Replay"/>, which prevents
    /// reusing a code that was already accepted.
    /// </summary>
    TotpValidationResult Validate(string base32Secret, string code, long minTimeStep, out long matchedTimeStep);
}
