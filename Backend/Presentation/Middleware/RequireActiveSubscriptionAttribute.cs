using Microsoft.AspNetCore.Mvc;

namespace Presentation.Middleware;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireActiveSubscriptionAttribute : ServiceFilterAttribute
{
    public RequireActiveSubscriptionAttribute() : base(typeof(RequireActiveSubscriptionFilter)) { }
}
