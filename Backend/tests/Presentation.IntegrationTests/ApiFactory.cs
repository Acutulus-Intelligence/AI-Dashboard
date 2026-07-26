using Application.Interfaces;
using Infrastructure.Ai.Services;
using Infrastructure.Payment;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Presentation.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _appDb = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("ai_dashboard_test")
        .WithUsername("actulus")
        .WithPassword("actulus_secret")
        .Build();

    private readonly PostgreSqlContainer _externalDb = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("external_sample")
        .WithUsername("extuser")
        .WithPassword("extpass")
        .Build();

    public FakePaymentService FakePayments { get; } = new();
    public FakeAiService FakeAi { get; } = new();

    public string ExternalHost => "127.0.0.1";
    public int ExternalPort => _externalDb.GetMappedPublicPort(5432);
    public string ExternalDatabase => "external_sample";
    public string ExternalUsername => "extuser";
    public string ExternalPassword => "extpass";

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_appDb.StartAsync(), _externalDb.StartAsync());
        await SeedExternalDatabaseAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _appDb.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__Secret", "integration-test-jwt-secret-key-at-least-32-chars!");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "ai-dashboard-test");
        Environment.SetEnvironmentVariable("Jwt__Audience", "ai-dashboard-client-test");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenExpirationMinutes", "60");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenExpirationDays", "7");
        Environment.SetEnvironmentVariable("Encryption__Key", "integration-test-encryption-key-32b!");
        Environment.SetEnvironmentVariable("Cors__AllowedOrigins__0", "http://localhost:5173");
        Environment.SetEnvironmentVariable("Stripe__SecretKey", "sk_test_fake");
        Environment.SetEnvironmentVariable("Stripe__WebhookSecret", "whsec_fake");
        Environment.SetEnvironmentVariable("Ai__ApiKey", "fake");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _appDb.DisposeAsync();
        await _externalDb.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _appDb.GetConnectionString(),
                ["Jwt:Secret"] = "integration-test-jwt-secret-key-at-least-32-chars!",
                ["Jwt:Issuer"] = "ai-dashboard-test",
                ["Jwt:Audience"] = "ai-dashboard-client-test",
                ["Jwt:AccessTokenExpirationMinutes"] = "60",
                ["Jwt:RefreshTokenExpirationDays"] = "7",
                ["Encryption:Key"] = "integration-test-encryption-key-32b!",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                ["Stripe:SecretKey"] = "sk_test_fake",
                ["Stripe:WebhookSecret"] = "whsec_fake",
                ["Ai:ApiKey"] = "fake",
                ["Ai:BaseUrl"] = "http://localhost",
                ["Ai:Model"] = "test-model",
                ["ExternalDb:PreviewMaxRows"] = "10",
                ["ExternalDb:QueryMaxRows"] = "10000",
                ["ExternalDb:QueryTimeoutSeconds"] = "30",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPaymentService>();
            services.AddSingleton<IPaymentService>(FakePayments);

            services.RemoveAll<IAiService>();
            services.RemoveAll<OpenRouterService>();
            services.AddSingleton<IAiService>(FakeAi);
        });
    }

    private async Task SeedExternalDatabaseAsync()
    {
        await using var conn = new Npgsql.NpgsqlConnection(_externalDb.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS sales (
                id SERIAL PRIMARY KEY,
                category TEXT NOT NULL,
                amount NUMERIC NOT NULL
            );
            DELETE FROM sales;
            INSERT INTO sales (category, amount) VALUES
                ('A', 10),
                ('B', 20),
                ('A', 15);
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
