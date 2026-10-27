namespace Application.Interfaces;

/// <summary>
/// Validates RFC 6238 time-based one-time passwords (TOTP) and reports which
/// time step a code matched, so callers can reject replays.
/// </summary>
public interface ITotpService
{
    /// <summary>
    /// Validates <paramref name="code"/> against <paramref name="base32Secret"/>.
    /// Codes whose time step is less than or equal to <paramref name="minTimeStep"/>
    /// are rejected, which prevents reusing a code that was already accepted.
    /// </summary>
    bool TryValidate(string base32Secret, string code, long minTimeStep, out long matchedTimeStep);
}
