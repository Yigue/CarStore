using System.Security.Claims;
using Application.Abstractions.Data;
using Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace InfrastructureTests.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private static PermissionAuthorizationHandler CreateHandler(Mock<IApplicationDbContext>? mockContext = null)
    {
        var services = new ServiceCollection();
        
        if (mockContext != null)
        {
            services.AddSingleton(mockContext.Object);
        }
        else
        {
            var mockDb = new Mock<IApplicationDbContext>();
            services.AddSingleton(mockDb.Object);
        }
        
        services.AddSingleton<PermissionProvider>();
        services.AddLogging(builder => builder.AddConsole());
        
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var logger = provider.GetRequiredService<ILogger<PermissionAuthorizationHandler>>();
        
        return new PermissionAuthorizationHandler(scopeFactory, logger);
    }

    [Fact]
    public async Task AuthenticatedUser_WithValidPermission_Succeeds()
    {
        var handler = CreateHandler();
        var requirement = new PermissionRequirement("cars:read");
        var userId = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "test"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task UnauthenticatedUser_Fails()
    {
        var handler = CreateHandler();
        var requirement = new PermissionRequirement("cars:read");
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task AuthenticatedUser_WithInvalidPermission_Fails()
    {
        var handler = CreateHandler();
        var requirement = new PermissionRequirement("nonexistent:permission");
        var userId = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "test"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }
}
