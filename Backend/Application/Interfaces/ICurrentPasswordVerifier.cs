using Domain.Models;

namespace Application.Interfaces;

public enum CurrentPasswordCheck
{
    Success,
    Invalid,
    LockedOut,
}

/// <summary>
/// Verifies a user's current password and registers failed attempts toward
/// Identity lockout.
/// </summary>
public interface ICurrentPasswordVerifier
{
    Task<CurrentPasswordCheck> VerifyAsync(User user, string password);
}
