namespace Application.Services;

/// <summary>
/// One repair attempt when post-generation grounding fails, so the model can
/// fix invented columns or invalid style values before the client sees an error.
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
            last = await generate(repairHint, ct);
            var error = validate(last);
            if (error is null)
            {
                if (attempt > 1)
                    notes.Add($"Attempt {attempt}: grounding succeeded after repair.");
                return (last, notes);
            }

            notes.Add($"Attempt {attempt}: grounding failed ({error}).");
            repairHint = error;
        }

        throw new InvalidOperationException(
            $"AI output failed schema/style grounding after {MaxAttempts} attempts. {repairHint}");
    }
}
