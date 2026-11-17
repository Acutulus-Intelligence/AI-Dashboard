using FluentValidation;

namespace Application.Validators;

public static class ValidatorExtensions
{
    /// <summary>
    /// Rejects values that could be abused for HTML/header injection when echoed
    /// back to users (company and role names are rendered in invitation emails).
    /// </summary>
    public static IRuleBuilderOptions<T, string> NoMarkupOrControlCharacters<T>(
        this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .Must(value => value.All(c => !char.IsControl(c) && c is not ('<' or '>')))
            .WithMessage("Must not contain HTML markup or control characters.");

    /// <summary>Shared password strength rules used by registration flows.</summary>
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(
        this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
}
