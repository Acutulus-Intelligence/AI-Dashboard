using System.Security.Cryptography;
using System.Text;

namespace Application.Services;

/// <summary>
/// Redacts SQL before it reaches logs. String literals and comments are masked so
/// the statement shape remains debuggable while values stay out of log storage.
/// </summary>
public static class SqlRedactor
{
    private const int MaxPreviewLength = 500;

    public static string Redact(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return string.Empty;

        var masked = SqlMasking.MaskLiteralsAndComments(
            sql,
            provider: null,
            maskAllLineComments: true);
        var sb = new StringBuilder(masked.Length);
        var previousWasSpace = false;

        foreach (var c in masked)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!previousWasSpace)
                {
                    sb.Append(' ');
                    previousWasSpace = true;
                }

                continue;
            }

            sb.Append(c);
            previousWasSpace = false;
        }

        var redacted = sb.ToString().Trim();
        return redacted.Length <= MaxPreviewLength
            ? redacted
            : redacted[..MaxPreviewLength] + "…";
    }

    /// <summary>Stable short fingerprint for correlating audit events.</summary>
    public static string Hash(string? sql)
    {
        if (string.IsNullOrEmpty(sql))
            return string.Empty;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sql));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
