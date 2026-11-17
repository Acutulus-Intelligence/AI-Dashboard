using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Application.Caching;
using Application.Common.Mapping;
using Application.Datasets;
using Application.Interfaces;
using Application.Services;
using Application.Settings;
using Application.Validators;
using Domain.Models;
using FluentValidation;
using FluentValidation.AspNetCore;
using Infrastructure.Auth;
using Infrastructure.Caching;
using Infrastructure.Data;
using Infrastructure.Ai;
using Infrastructure.Ai.Services;
using Infrastructure.Encryption;
using Infrastructure.Email;
using Infrastructure.ExternalDb;
using Infrastructure.ExternalDb.Services;
using Infrastructure.Collections;
using Infrastructure.Datasets;
using Infrastructure.HostedServices;
using Infrastructure.Payment;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Presentation.Middleware;
using Presentation.Services;
using Resend;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi("v1", options => { options.AddDocumentTransformer<BearerSecuritySchemeTransformer>(); });

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

builder.Services.AddIdentity<User, IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.AllowedForNewUsers = true;
});

var jwtSettings = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtSettings>(jwtSettings);

var jwtSecret = jwtSettings["Secret"]
    ?? throw new InvalidOperationException("JWT Secret is not configured.");

builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ClockSkew = TimeSpan.Zero,
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var header = context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(header) && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Token = header["Bearer ".Length..].Trim();
                return Task.CompletedTask;
            }

            context.Token = context.Request.Cookies["access_token"];
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddSingleton<IExceptionMapper, ExceptionMapper>();

// HTTP client for AI provider calls
builder.Services.AddHttpClient();

// Encryption
builder.Services.Configure<EncryptionSettings>(builder.Configuration.GetSection("Encryption"));
builder.Services.AddSingleton<IEncryptionService, AesEncryptionService>();

// Chart result cache: in-process L1 -> Redis L2 -> query/DB.
var chartCacheSection = builder.Configuration.GetSection(ChartCacheSettings.SectionName);
builder.Services.Configure<ChartCacheSettings>(chartCacheSection);
var chartCacheSettings = chartCacheSection.Get<ChartCacheSettings>() ?? new ChartCacheSettings();
builder.Services.AddMemoryCache(options =>
{
    if (chartCacheSettings.L1SizeLimitBytes > 0)
        options.SizeLimit = chartCacheSettings.L1SizeLimitBytes;
});
builder.Services.AddSingleton<IRedisConnectionProvider, RedisConnectionProvider>();
builder.Services.AddSingleton<RedisChartResultCache>();
builder.Services.AddSingleton<IChartResultCache>(sp => sp.GetRequiredService<RedisChartResultCache>());
builder.Services.AddSingleton<IChartCacheInvalidationHandler>(sp => sp.GetRequiredService<RedisChartResultCache>());
builder.Services.AddHostedService<ChartCacheInvalidationSubscriber>();
builder.Services.AddScoped<IChartCacheInvalidator, ChartCacheInvalidator>();
builder.Services.AddSingleton<IKeyedAsyncLock, KeyedAsyncLock>();

// External database connections
builder.Services.AddScoped<IExternalConnectionService, ExternalConnectionService>();
builder.Services.AddScoped<ISchemaInspector, SchemaInspector>();
builder.Services.AddSingleton<ISqlValidator, SqlValidator>();
builder.Services.AddScoped<IQueryExecutor, QueryExecutor>();
builder.Services.AddScoped<IConnectionAccessService, ConnectionAccessService>();

// AI service
builder.Services.Configure<AiSettings>(builder.Configuration.GetSection("Ai"));
builder.Services.AddHttpClient<IAiService, OpenRouterService>();

// Email delivery (Resend)
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));
builder.Services.AddResend(options =>
{
    options.ApiToken = builder.Configuration["Email:ResendApiKey"] ?? string.Empty;
});
builder.Services.AddScoped<IEmailSender, ResendEmailSender>();

// Graph generation
builder.Services.AddScoped<IGraphGenerationService, GraphGenerationService>();
builder.Services.AddScoped<IChartService, ChartService>();
builder.Services.AddScoped<IChartFolderService, ChartFolderService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Application layer
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IPaymentService, StripeService>();
builder.Services.AddScoped<IAccountDeletionService, AccountDeletionService>();
builder.Services.AddScoped<RequireActiveSubscriptionFilter>();

builder.Services.AddHostedService<AccountPurgeBackgroundService>();

builder.Services.AddScoped<ITokenService, JwtService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<ICurrentPasswordVerifier, CurrentPasswordVerifier>();
builder.Services.AddScoped<ITotpService, TotpService>();


builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

builder.Services.Configure<ExternalDbSettings>(builder.Configuration.GetSection("ExternalDb"));
builder.Services.Configure<DatasetSettings>(builder.Configuration.GetSection(DatasetSettings.SectionName));
builder.Services.AddSingleton<IDatasetFileParser, CsvFileParser>();
builder.Services.AddSingleton<IDatasetFileParser, XlsxFileParser>();
builder.Services.AddScoped<IDatasetQueryExecutor, SqliteDatasetExecutor>();
builder.Services.AddScoped<IDataQueryExecutor, InMemoryDataQueryExecutor>();
builder.Services.AddScoped<ICollectionAccessService, CollectionAccessService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();

// Behind a reverse proxy the client IP and scheme arrive via forwarded
// headers. Without this, rate limiting keys every request to the proxy IP and
// auth cookies miss the Secure flag. Opt in via config because trusting
// forwarded headers from an untrusted client lets it spoof both.
var forwardedHeadersSection = builder.Configuration.GetSection("ForwardedHeaders");
var forwardedHeadersEnabled = forwardedHeadersSection.GetValue("Enabled", false);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    foreach (var proxy in forwardedHeadersSection.GetSection("KnownProxies").Get<string[]>() ?? [])
    {
        if (IPAddress.TryParse(proxy, out var ip))
            options.KnownProxies.Add(ip);
    }

    // Trust any hop (dynamic PaaS proxies). Only safe when traffic cannot reach
    // the app directly, bypassing the proxy.
    if (forwardedHeadersSection.GetValue("TrustAllProxies", false))
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
});

var corsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(corsOrigins)
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Content-Type", "Authorization")
            .WithExposedHeaders("X-Company-Connection-Count")
            .AllowCredentials();
    });
});

var rateLimitingEnabled = builder.Configuration.GetValue("RateLimiting:Enabled", true);

if (rateLimitingEnabled)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = async (context, token) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                context.HttpContext.Response.Headers.RetryAfter =
                    Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { detail = "Too many requests. Please wait and try again." },
                token);
        };

        options.AddPolicy("login", httpContext =>
        {
            // Authenticated management calls (2FA setup/disable, account
            // deletion, ...) are partitioned per user so one user cannot exhaust
            // a shared proxy-IP budget for everyone. Anonymous logins still key
            // on the (forwarded) client IP.
            var userId = httpContext.User?.FindFirst("userId")?.Value;
            var partitionKey = !string.IsNullOrEmpty(userId)
                ? $"user:{userId}"
                : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

            var permitLimit = builder.Configuration.GetValue(
                "RateLimiting:Login:PermitLimit", 10);
            var windowSeconds = builder.Configuration.GetValue(
                "RateLimiting:Login:WindowSeconds", 60);

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: partitionKey,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, permitLimit),
                    Window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds)),
                    QueueLimit = 0,
                });
        });

        // Guards invitation creation/resend so a compromised or careless admin
        // account cannot spam the Resend quota or flood inboxes.
        options.AddPolicy("invite", httpContext =>
        {
            var userId = httpContext.User?.FindFirst("userId")?.Value;
            var partitionKey = !string.IsNullOrEmpty(userId)
                ? $"user:{userId}"
                : $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

            var permitLimit = builder.Configuration.GetValue(
                "RateLimiting:Invite:PermitLimit", 20);
            var windowSeconds = builder.Configuration.GetValue(
                "RateLimiting:Invite:WindowSeconds", 3600);

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: partitionKey,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, permitLimit),
                    Window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds)),
                    QueueLimit = 0,
                });
        });

        // Guards the public invitation validate/accept endpoints (token guessing).
        options.AddPolicy("invite-accept", httpContext =>
        {
            var partitionKey = $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

            var permitLimit = builder.Configuration.GetValue(
                "RateLimiting:InviteAccept:PermitLimit", 15);
            var windowSeconds = builder.Configuration.GetValue(
                "RateLimiting:InviteAccept:WindowSeconds", 60);

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: partitionKey,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, permitLimit),
                    Window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds)),
                    QueueLimit = 0,
                });
        });

        // Every forced execution is rate-limited. The client cannot be trusted
        // to distinguish "manual" from "auto": that would be a trivial bypass.
        options.AddPolicy("chart-refresh", httpContext =>
        {
            var forceRefresh = bool.TryParse(
                httpContext.Request.Query["forceRefresh"],
                out var force) && force;

            var userId = httpContext.User?.FindFirst("userId")?.Value;

            if (!forceRefresh)
            {
                var executePartitionKey = !string.IsNullOrEmpty(userId)
                    ? $"chart-execute:user:{userId}"
                    : $"chart-execute:ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

                var executePermitLimit = builder.Configuration.GetValue(
                    "RateLimiting:ChartExecute:PermitLimit", 1200);
                var executeWindowSeconds = builder.Configuration.GetValue(
                    "RateLimiting:ChartExecute:WindowSeconds", 60);

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: executePartitionKey,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, executePermitLimit),
                        Window = TimeSpan.FromSeconds(Math.Max(1, executeWindowSeconds)),
                        QueueLimit = 0,
                    });
            }

            var partitionKey = !string.IsNullOrEmpty(userId)
                ? $"chart-refresh:user:{userId}"
                : $"chart-refresh:ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

            var permitLimit = builder.Configuration.GetValue(
                "RateLimiting:ChartRefresh:PermitLimit", 300);
            var windowSeconds = builder.Configuration.GetValue(
                "RateLimiting:ChartRefresh:WindowSeconds", 60);

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: partitionKey,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, permitLimit),
                    Window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds)),
                    QueueLimit = 0,
                });
        });
    });
}

var app = builder.Build();

if (string.IsNullOrWhiteSpace(builder.Configuration.GetValue<string>("Email:ResendApiKey")))
{
    app.Logger.LogWarning(
        "Email:ResendApiKey is not configured; email invitations will fail until it is set.");
}

if (forwardedHeadersEnabled)
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<GlobalExceptionMiddleware>();

app.Use(async (context, next) =>
{
    // Keep invite/recovery tokens and other URLs out of the Referer header (S3).
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.AddPreferredSecuritySchemes("Bearer")
            .AddHttpAuthentication("Bearer", auth =>
            {
                auth.Token = "";
            });
    });
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    await SeedData.InitializeAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");

app.UseAuthentication();
app.UseMiddleware<UserExistsMiddleware>();
app.UseAuthorization();

if (rateLimitingEnabled)
{
    app.UseRateLimiter();
}

app.MapControllers();

await app.RunAsync();

public partial class Program;
