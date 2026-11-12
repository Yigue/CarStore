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
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("dealer_id", dealerId.ToString())
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

    // sfc B6.3 security review: a user's permissions are now dealer-scoped (UserDealerMembership can give a
    // DIFFERENT role per dealer). The dealer_id claim on the ACTIVE token must decide which role's permissions
    // apply — not always the user's home role — or switching dealers would leak the home dealer's grants.
    [Fact]
    public async Task UserWithADifferentRoleInAnotherDealer_GetsThatDealersPermissions_NotTheHomeOnes()
    {
        var homeDealerId = Guid.NewGuid();
        var otherDealerId = Guid.NewGuid();

        var homeAdminRole = new Domain.Users.Role(homeDealerId, "Admin", "Admin Role");
        homeAdminRole.AddPermission("CanManageUsers");
        homeAdminRole.AddPermission("cars:delete");
        _context.Roles.Add(homeAdminRole);

        var otherLimitedRole = new Domain.Users.Role(otherDealerId, "Empleado", "Empleado Role");
        otherLimitedRole.AddPermission("cars:read");
        _context.Roles.Add(otherLimitedRole);

        var userEntity = new Domain.Users.User(homeDealerId, "multi@example.com", "Multi", "Dealer", "hash", homeAdminRole.Id);
        _context.Users.Add(userEntity);
        await _context.SaveChangesAsync();

        _context.UserDealerMemberships.Add(
            Domain.Users.UserDealerMembership.Grant(userEntity.Id, otherDealerId, otherLimitedRole.Id, DateTime.UtcNow));
        await _context.SaveChangesAsync();

        var handler = CreateHandler();

        // Acting as the OTHER dealer (dealer_id claim says so) — must get Empleado's permissions, never Admin's.
        var requirement = new PermissionRequirement("CanManageUsers");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userEntity.Id.ToString()),
            new Claim("dealer_id", otherDealerId.ToString())
        }, "test"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse(
            "scoped to the other dealer, this user is only Empleado there and must not keep the home Admin grant");
    }

    [Fact]
    public async Task UserScopedToADealerTheyHaveNoMembershipIn_GetsNoRoleBasedPermissions()
    {
        var homeDealerId = Guid.NewGuid();
        var unrelatedDealerId = Guid.NewGuid();

        var homeAdminRole = new Domain.Users.Role(homeDealerId, "Admin", "Admin Role");
        homeAdminRole.AddPermission("cars:read");
        _context.Roles.Add(homeAdminRole);

        var userEntity = new Domain.Users.User(homeDealerId, "solo@example.com", "Solo", "Dealer", "hash", homeAdminRole.Id);
        _context.Users.Add(userEntity);
        await _context.SaveChangesAsync();

        var handler = CreateHandler();
        var requirement = new PermissionRequirement("cars:read");
        // A stale/forged/mismatched dealer_id claim with no membership — must deny, never fall back to home.
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userEntity.Id.ToString()),
            new Claim("dealer_id", unrelatedDealerId.ToString())
        }, "test"));
        var context = new AuthorizationHandlerContext(new[] { requirement }, user, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }
}
