using System.Security.Claims;
using Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace InfrastructureTests.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private static PermissionAuthorizationHandler CreateHandler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<PermissionProvider>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        return new PermissionAuthorizationHandler(scopeFactory);
    }

    [Fact]
    public async Task AuthenticatedUser_Succeeds()
    {
        var handler = CreateHandler();
        var requirement = new PermissionRequirement("perm");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, "test"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task UnauthenticatedUser_Fails()
    {
        var handler = CreateHandler();
        var requirement = new PermissionRequirement("perm");
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }
}
