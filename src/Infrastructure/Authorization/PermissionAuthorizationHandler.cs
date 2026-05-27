using Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Authorization;

public sealed class PermissionAuthorizationHandler(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PermissionAuthorizationHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            logger.LogWarning(
                "Permission authorization failed: user is not authenticated. Required: {Permission}",
                requirement.Permission);
            return;
        }

        Guid userId;
        try
        {
            userId = context.User.GetUserId();
        }
        catch (ApplicationException)
        {
            string claimDump = string.Join(", ", context.User.Claims.Select(c => $"{c.Type}={c.Value}"));
            logger.LogError(
                "Permission denied: could not extract userId from claims. Required: {Permission}. Claims: [{Claims}]",
                requirement.Permission,
                claimDump);
            return;
        }

        using IServiceScope scope = serviceScopeFactory.CreateScope();
        PermissionProvider permissionProvider = scope.ServiceProvider.GetRequiredService<PermissionProvider>();

        HashSet<string> permissions = await permissionProvider.GetForUserIdAsync(userId);

        if (permissions.Contains(requirement.Permission))
        {
            logger.LogDebug(
                "Permission granted for user {UserId}. Permission: {Permission}",
                userId,
                requirement.Permission);
            context.Succeed(requirement);
            return;
        }

        logger.LogWarning(
            "Permission denied for user {UserId}. Required: {Permission}. User has {Count} permissions: [{Permissions}]",
            userId,
            requirement.Permission,
            permissions.Count,
            string.Join(", ", permissions));
    }
}
