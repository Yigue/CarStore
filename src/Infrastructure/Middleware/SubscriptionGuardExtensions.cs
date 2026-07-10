using Microsoft.AspNetCore.Builder;

namespace Infrastructure.Middleware;

public static class SubscriptionGuardExtensions
{
    public static IApplicationBuilder UseSubscriptionGuard(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SubscriptionGuardMiddleware>();
    }
}
