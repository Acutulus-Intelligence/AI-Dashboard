namespace Application.Services;

/// <summary>
/// One repair attempt when post-generation grounding fails, so the model can
/// fix invented columns or invalid style values before the client sees an error.
/// Provider exceptions (unknown chart type, empty JSON) are treated the same way.
/// </summary>
public static class AiGenerationRetry
{
    public const int MaxAttempts = 2;

    public static async Task<(T Result, List<string> Notes)> RunAsync<T>(
        Func<string?, CancellationToken, Task<T>> generate,
        Func<T, string?> validate,
        CancellationToken ct = default)
    {
        var notes = new List<string>();
        string? repairHint = null;
        T? last = default;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                last = await generate(repairHint, ct);
            }
            catch (InvalidOperationException ex) when (attempt < MaxAttempts)
            {
                notes.Add($"Attempt {attempt}: generation failed ({ex.Message}).");
                repairHint = ex.Message;
                continue;
            }

            var error = validate(last!);
            if (error is null)
            {
                if (attempt > 1)
                    notes.Add($"Attempt {attempt}: grounding succeeded after repair.");
                return (last!, notes);
            }

            notes.Add($"Attempt {attempt}: grounding failed ({error}).");
            repairHint = error;
        }

        throw new InvalidOperationException(
            $"AI output failed schema/style grounding after {MaxAttempts} attempts. {repairHint}");
    }

    /// <summary>
    /// True when a database exception looks like a missing table/column rather than
    /// a timeout, permission, or row-limit failure.
    /// </summary>
    public static bool IsLikelySchemaError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var m = message.ToLowerInvariant();
        return m.Contains("does not exist", StringComparison.Ordinal)
            || m.Contains("unknown column", StringComparison.Ordinal)
            || m.Contains("no such column", StringComparison.Ordinal)
            || m.Contains("invalid column", StringComparison.Ordinal)
            || m.Contains("undefined column", StringComparison.Ordinal)
            || m.Contains("no such table", StringComparison.Ordinal)
            || m.Contains("unknown table", StringComparison.Ordinal)
            || m.Contains("invalid object name", StringComparison.Ordinal)
            || m.Contains("invalid object", StringComparison.Ordinal)
            || (m.Contains("relation", StringComparison.Ordinal) && m.Contains("does not", StringComparison.Ordinal))
            || (m.Contains("column", StringComparison.Ordinal) && m.Contains("not exist", StringComparison.Ordinal))
            || (m.Contains("table", StringComparison.Ordinal) && m.Contains("not exist", StringComparison.Ordinal));
    }
}
