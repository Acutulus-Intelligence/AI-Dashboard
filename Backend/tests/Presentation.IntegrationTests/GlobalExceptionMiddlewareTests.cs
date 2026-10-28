using System.Text.Json;
using Application.Common.Mapping;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Presentation.Middleware;

namespace Presentation.IntegrationTests;

public sealed class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task Client_error_message_is_exposed_in_production()
    {
        var body = await InvokeAsync(
            new UnauthorizedAccessException("Invalid email or password."),
            environmentName: Environments.Production);

        body.Status.Should().Be(StatusCodes.Status401Unauthorized);
        body.Detail.Should().Be("Invalid email or password.");
    }

    [Fact]
    public async Task Server_error_message_is_masked_in_production()
    {
        var body = await InvokeAsync(
            new InvalidTimeZoneException("internal detail that must not leak"),
            environmentName: Environments.Production);

        body.Status.Should().Be(StatusCodes.Status500InternalServerError);
        body.Detail.Should().Be("An unexpected error occurred.");
    }

    [Fact]
    public void Db_update_unique_violation_maps_to_conflict()
    {
        var exception = DbUpdateError(PostgresErrorCodes.UniqueViolation);

        new ExceptionMapper().Map(exception).StatusCode
            .Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void Db_update_other_failures_map_to_server_error()
    {
        var exception = DbUpdateError(PostgresErrorCodes.NotNullViolation);

        new ExceptionMapper().Map(exception).StatusCode
            .Should().Be(StatusCodes.Status500InternalServerError);
    }

    private static DbUpdateException DbUpdateError(string sqlState)
        => new("database update failed",
            new PostgresException("database update failed", "ERROR", "ERROR", sqlState));

    private static async Task<(int Status, string? Detail)> InvokeAsync(
        Exception exception, string environmentName)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new GlobalExceptionMiddleware(
            _ => throw exception,
            NullLogger<GlobalExceptionMiddleware>.Instance,
            new FakeWebHostEnvironment(environmentName),
            new ExceptionMapper());

        await middleware.Invoke(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        using var document = JsonDocument.Parse(json);

        var detail = document.RootElement.TryGetProperty("detail", out var value)
            ? value.GetString()
            : null;

        return (context.Response.StatusCode, detail);
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public FakeWebHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; }
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
