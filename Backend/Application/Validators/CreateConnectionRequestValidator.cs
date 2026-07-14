using System.Text.RegularExpressions;
using Application.DTos.Request;
using FluentValidation;

namespace Application.Validators;

public partial class CreateConnectionRequestValidator : AbstractValidator<CreateConnectionRequest>
{
    private static readonly string[] BlockedHosts =
    [
        "169.254.169.254",
        "metadata.google.internal"
    ];

    public CreateConnectionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.DbProvider)
            .IsInEnum();

        RuleFor(x => x.Host)
            .NotEmpty()
            .MaximumLength(255)
            .Must(BeValidHost)
            .WithMessage("Host must be a valid hostname or IP address.")
            .Must(host => !IsBlockedHost(host))
            .WithMessage("This host is not allowed.");

        RuleFor(x => x.Port)
            .InclusiveBetween(1, 65535);

        RuleFor(x => x.Database)
            .NotEmpty()
            .MaximumLength(100)
            .Must(value => !ContainsConnectionStringDelimiter(value))
            .WithMessage("Database name contains invalid characters.");

        RuleFor(x => x.Username)
            .NotEmpty()
            .MaximumLength(100)
            .Must(value => !ContainsConnectionStringDelimiter(value))
            .WithMessage("Username contains invalid characters.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MaximumLength(500);
    }

    private static bool BeValidHost(string host)
    {
        if (ContainsConnectionStringDelimiter(host))
            return false;

        return HostPattern().IsMatch(host);
    }

    private static bool IsBlockedHost(string host)
    {
        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        return BlockedHosts.Any(blocked =>
            normalized.Equals(blocked, StringComparison.Ordinal));
    }

    private static bool ContainsConnectionStringDelimiter(string value) =>
        value.Contains(';') || value.Contains('=');

    [GeneratedRegex(@"^[a-zA-Z0-9]([a-zA-Z0-9\-\.]*[a-zA-Z0-9])?$|^(\d{1,3}\.){3}\d{1,3}$|^([0-9a-fA-F]{0,4}:){2,7}[0-9a-fA-F]{0,4}$")]
    private static partial Regex HostPattern();
}
