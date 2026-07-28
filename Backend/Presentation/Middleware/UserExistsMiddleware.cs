using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Presentation.Middleware;

public class UserExistsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserExistsMiddleware> _logger;

    public UserExistsMiddleware(RequestDelegate next, ILogger<UserExistsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is null) { await _next(context); return; }
        var authorizeData = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>() ?? [];
        var allowAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        if (authorizeData.Count == 0 || allowAnonymous)
        {
            await _next(context);
            return;
        }

        var userIdClaim = context.User.FindFirst("userId")?.Value;
        if (userIdClaim is not null && Guid.TryParse(userIdClaim, out var userId))
        {
            var db = context.RequestServices.GetRequiredService<IApplicationDbContext>();
            var user = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
            {
                _logger.LogWarning("Rejected request from deleted user {UserId}", userId);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "User account no longer exists." });
                return;
            }

            var tokenStamp = context.User.FindFirst("securityStamp")?.Value;
            if (!string.IsNullOrEmpty(tokenStamp) && tokenStamp != user.SecurityStamp)
            {
                _logger.LogWarning("Rejected request with stale security stamp for user {UserId}", userId);
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { error = "Session expired. Please log in again." });
                return;
            }
        }

        await _next(context);
    }
}
