using System.Security.Cryptography;
using Application.Interfaces;

namespace Infrastructure.Auth;

/// <summary>
/// RFC 6238 TOTP verification (HMAC-SHA1, 6 digits, 30 second steps) compatible
/// with the secrets produced by ASP.NET Identity's authenticator key generator.
/// </summary>
public class TotpService : ITotpService
{
    private const int StepSeconds = 30;
    private const int Digits = 6;
    private const int Window = 1;
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public TotpValidationResult Validate(string base32Secret, string code, long minTimeStep, out long matchedTimeStep)
    {
        matchedTimeStep = 0;

        if (string.IsNullOrWhiteSpace(base32Secret) || !IsSixDigitCode(code))
            return TotpValidationResult.Invalid;

        var key = Base32Decode(base32Secret);
        if (key.Length == 0)
            return TotpValidationResult.Invalid;

        var currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;
        var normalizedCode = code.Trim();
        var replay = false;

        for (var offset = -Window; offset <= Window; offset++)
        {
            var step = currentStep + offset;

            var candidate = ComputeCode(key, step);
            if (!CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(candidate),
                    System.Text.Encoding.ASCII.GetBytes(normalizedCode)))
            {
                continue;
            }

            // The code is correct for this step, but if the step was already
            // accepted the code is a replay rather than a fresh match.
            if (step <= minTimeStep)
            {
                replay = true;
                continue;
            }

            matchedTimeStep = step;
            return TotpValidationResult.Valid;
        }

        return replay ? TotpValidationResult.Replay : TotpValidationResult.Invalid;
    }

    private static bool IsSixDigitCode(string code)
    {
        var trimmed = code.Trim();
        if (trimmed.Length != Digits)
            return false;

        foreach (var c in trimmed)
        {
            if (c < '0' || c > '9')
                return false;
        }

        return true;
    }

    private static string ComputeCode(byte[] key, long timeStep)
    {
        var data = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(data);

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(data);

        var offset = hash[^1] & 0x0f;
        var binary =
            ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);

        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string input)
    {
        var cleaned = input.Trim().TrimEnd('=').Replace(" ", string.Empty).ToUpperInvariant();
        var bytes = new List<byte>(cleaned.Length * 5 / 8);
        var buffer = 0;
        var bits = 0;

        foreach (var c in cleaned)
        {
            var index = Base32Alphabet.IndexOf(c);
            if (index < 0)
                return [];

            buffer = (buffer << 5) | index;
            bits += 5;

            if (bits >= 8)
            {
                bytes.Add((byte)((buffer >> (bits - 8)) & 0xff));
                bits -= 8;
            }
        }

        return bytes.ToArray();
    }
}
