using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Presentation.Middleware;

public class RequireActiveSubscriptionFilter : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata
            .Any(m => m is AllowSubscriptionBypassAttribute))
            return;

        var userId = context.HttpContext.User.FindFirst("userId")?.Value;
        if (userId is null || !Guid.TryParse(userId, out var parsed))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var subscriptionService = context.HttpContext.RequestServices
            .GetRequiredService<ISubscriptionService>();

        var hasActive = await subscriptionService.HasActiveSubscriptionAsync(parsed);

        if (!hasActive)
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
    }
}
