using Application.Interfaces;
using Domain.Models;
using Microsoft.AspNetCore.Identity;

namespace Presentation.Services;

/// <summary>
/// Adapts Identity's <see cref="SignInManager{TUser}"/> (available in the web
/// host's shared framework) to the Application password-verification port.
/// </summary>
public class CurrentPasswordVerifier : ICurrentPasswordVerifier
{
    private readonly SignInManager<User> _signInManager;

    public CurrentPasswordVerifier(SignInManager<User> signInManager)
    {
        _signInManager = signInManager;
    }

    public async Task<CurrentPasswordCheck> VerifyAsync(User user, string password)
    {
        var result = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

        if (result.Succeeded)
            return CurrentPasswordCheck.Success;
        if (result.IsLockedOut)
            return CurrentPasswordCheck.LockedOut;

        return CurrentPasswordCheck.Invalid;
    }
}
