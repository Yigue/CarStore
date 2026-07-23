using System.Security.Claims;
using Application.Abstractions.Data;
using Infrastructure.Authorization;
using Infrastructure.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Threading;
using Infrastructure.Database;
using SharedKernel;
using Microsoft.Data.Sqlite;

namespace InfrastructureTests.Authorization;

public class PermissionAuthorizationHandlerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly SqliteConnection _connection;

    public PermissionAuthorizationHandlerTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var mockPublisher = new Mock<MediatR.IPublisher>();
        var mockTenant = new Mock<Application.Abstractions.Tenancy.ICurrentTenantService>();
        mockTenant.Setup(t => t.HasTenant).Returns(false);

        _context = new ApplicationDbContext(options, mockPublisher.Object, mockTenant.Object);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private PermissionAuthorizationHandler CreateHandler()
    {
        var services = new ServiceCollection();
        
        var mockCache = new Mock<ICacheService>();
        services.AddSingleton(mockCache.Object);
        
        services.AddSingleton<ILogger<PermissionProvider>>(NullLogger<PermissionProvider>.Instance);
        
        services.AddSingleton<IApplicationDbContext>(_context);
        services.AddScoped<PermissionProvider>();

        var logger = new Mock<ILogger<PermissionAuthorizationHandler>>();
        var handler = new PermissionAuthorizationHandler(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), logger.Object);

        return handler;
    }

    [Fact]
    public async Task AuthenticatedUser_WithValidPermission_Succeeds()
    {
        var permission = "cars:read";
        var requirement = new PermissionRequirement(permission);
        
        var dealerId = Guid.NewGuid();
        var role = new Domain.Users.Role(dealerId, "Admin", "Admin Role");
        role.AddPermission(permission);
        _context.Roles.Add(role);

        var userEntity = new Domain.Users.User(dealerId, "test@example.com", "Test", "User", "hashedPassword", role.Id);
        var userId = userEntity.Id;
        _context.Users.Add(userEntity);
        await _context.SaveChangesAsync();

        var handler = CreateHandler();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "test"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task AuthenticatedUser_WithInvalidPermission_Fails()
    {
        var userId = Guid.NewGuid();
        var requirement = new PermissionRequirement("cars:write");
        
        var handler = CreateHandler();
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }, "test"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }
}
