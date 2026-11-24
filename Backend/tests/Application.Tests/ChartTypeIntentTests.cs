using Application.Services;
using FluentAssertions;

namespace Application.Tests;

public class ChartTypeIntentTests
{
    [Theory]
    [InlineData("create a pie chart")]
    [InlineData("as a line over time")]
    [InlineData("show it as a table")]
    [InlineData("visa som stapel")]
    [InlineData("gör en paj")]
    [InlineData("kpi för intäkterna")]
    public void Detects_named_chart_types(string prompt)
    {
        ChartTypeIntent.PromptNamesChartType(prompt).Should().BeTrue();
    }

    [Theory]
    [InlineData("show sales by category")]
    [InlineData("revenue last month")]
    [InlineData("top 10 customers")]
    [InlineData("deadline per project")]
    [InlineData(null)]
    public void Ignores_prompts_without_a_type(string? prompt)
    {
        ChartTypeIntent.PromptNamesChartType(prompt).Should().BeFalse();
    }
}
