using Application.Services;
using FluentAssertions;

namespace Application.Tests;

public class AiGenerationRetryTests
{
    [Fact]
    public async Task Succeeds_on_first_valid_result()
    {
        var calls = 0;

        var (result, notes) = await AiGenerationRetry.RunAsync(
            (_, _) =>
            {
                calls++;
                return Task.FromResult("ok");
            },
            value => value == "ok" ? null : "bad");

        result.Should().Be("ok");
        calls.Should().Be(1);
        notes.Should().BeEmpty();
    }

    [Fact]
    public async Task Retries_once_when_first_result_fails_grounding()
    {
        var calls = 0;
        string? seenHint = null;

        var (result, notes) = await AiGenerationRetry.RunAsync(
            (hint, _) =>
            {
                calls++;
                seenHint = hint;
                return Task.FromResult(calls == 1 ? "invented_column" : "ok");
            },
            value => value == "ok" ? null : "Unknown column(s) in SQL: customer_email.");

        result.Should().Be("ok");
        calls.Should().Be(2);
        seenHint.Should().Contain("customer_email");
        notes.Should().Contain(n => n.Contains("Attempt 1"));
        notes.Should().Contain(n => n.Contains("Attempt 2"));
    }

    [Fact]
    public async Task Throws_after_max_failed_attempts()
    {
        var act = () => AiGenerationRetry.RunAsync(
            (_, _) => Task.FromResult("bad"),
            _ => "Unknown column(s) in SQL: revenue.");

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("revenue");
        ex.Which.Message.Should().Contain("2 attempts");
    }

    [Fact]
    public async Task Passes_repair_hint_only_on_retry()
    {
        var hints = new List<string?>();

        await AiGenerationRetry.RunAsync(
            (hint, _) =>
            {
                hints.Add(hint);
                return Task.FromResult(hints.Count == 1 ? "bad" : "ok");
            },
            value => value == "ok" ? null : "fix the columns");

        hints.Should().Equal(null, "fix the columns");
    }
}
