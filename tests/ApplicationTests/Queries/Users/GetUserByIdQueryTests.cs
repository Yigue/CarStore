using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions.Tenancy;
using Application.Users.GetById;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Queries.Users;

public class GetUserByIdQueryTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static GetUserByIdQueryHandler CreateHandler(
        TestApplicationDbContext context,
        ICurrentTenantService? tenantService = null)
    {
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(Guid.NewGuid());

        return new GetUserByIdQueryHandler(
            context,
            tenantService ?? mockTenantService.Object);
    }

    [Fact]
    public async Task Handle_ValidQuery_ReturnsUser()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var user = new User(dealerId, "user@example.com", "John", "Doe", "hash", UserRole.Empleado);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);
        var query = new GetUserByIdQuery(user.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(user.Id);
        result.Value.Email.Should().Be("user@example.com");
        result.Value.FirstName.Should().Be("John");
        result.Value.LastName.Should().Be("Doe");
        result.Value.Role.Should().Be(UserRole.Empleado);
    }

    [Fact]
    public async Task Handle_NonExistentUser_ReturnsNotFoundError()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);
        var query = new GetUserByIdQuery(Guid.NewGuid());

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(query.UserId));
    }

    [Fact]
    public async Task Handle_TenantIsolation_CannotGetOtherDealerUser()
    {
        using var context = CreateContext();
        var dealerId1 = Guid.NewGuid();
        var dealerId2 = Guid.NewGuid();

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId1);

        var user = new User(dealerId2, "other@example.com", "Other", "Dealer", "hash");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);
        var query = new GetUserByIdQuery(user.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound(user.Id));
    }

    [Fact]
    public async Task Handle_ReturnsUserWithPermissions()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var user = new User(dealerId, "perm@example.com", "Perm", "User", "hash", UserRole.Empleado);
        user.AddPermission("users:read");
        user.AddPermission("users:write");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);
        var query = new GetUserByIdQuery(user.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Permissions.Should().Contain("users:read");
        result.Value.Permissions.Should().Contain("users:write");
    }

    [Fact]
    public async Task Handle_ReturnsUserWithPhone()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var user = new User(dealerId, "phone@example.com", "Phone", "User", "hash");
        user.UpdatePhone("+5491112345678");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);
        var query = new GetUserByIdQuery(user.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Phone.Should().Be("+5491112345678");
    }

    [Fact]
    public async Task Handle_InactiveUser_ReturnsUserWithIsActiveFalse()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var user = new User(dealerId, "inactive@example.com", "Inactive", "User", "hash", UserRole.Empleado);
        user.Deactivate();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);
        var query = new GetUserByIdQuery(user.Id);

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsActive.Should().BeFalse();
    }
}