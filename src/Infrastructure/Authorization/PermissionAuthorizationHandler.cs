using Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Authorization;

internal sealed class PermissionAuthorizationHandler(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<PermissionAuthorizationHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Rechazar usuarios no autenticados explícitamente
        if (context.User.Identity?.IsAuthenticated != true)
        {
            logger.LogWarning(
                "Permission authorization failed: User is not authenticated. Required permission: {Permission}",
                requirement.Permission);
            return;
        }

        using IServiceScope scope = serviceScopeFactory.CreateScope();

        PermissionProvider permissionProvider = scope.ServiceProvider.GetRequiredService<PermissionProvider>();

        Guid userId = context.User.GetUserId();

        logger.LogDebug(
            "Checking permission for user {UserId}. Required permission: {Permission}",
            userId,
            requirement.Permission);

        HashSet<string> permissions = await permissionProvider.GetForUserIdAsync(userId);

        if (permissions.Contains(requirement.Permission))
        {
            logger.LogDebug(
                "Permission granted for user {UserId}. Permission: {Permission}",
                userId,
                requirement.Permission);
            context.Succeed(requirement);
        }
        else
        {
            logger.LogWarning(
                "Permission denied for user {UserId}. Required permission: {Permission}. User has {PermissionCount} permissions",
                userId,
                requirement.Permission,
                permissions.Count);
        }
    }
}
