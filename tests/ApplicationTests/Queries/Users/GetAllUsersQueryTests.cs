using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Abstractions.Tenancy;
using Application.Users.Queries.GetAllUsers;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Application.UnitTests.Queries.Users;

public class GetAllUsersQueryTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static GetAllUsersQueryHandler CreateHandler(
        TestApplicationDbContext context,
        ICurrentTenantService? tenantService = null)
    {
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(Guid.NewGuid());

        return new GetAllUsersQueryHandler(
            context,
            tenantService ?? mockTenantService.Object);
    }

    [Fact]
    public async Task Handle_DefaultQuery_ReturnsPaginatedUsers()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        context.Users.Add(new User(dealerId, "alice@example.com", "Alice", "Johnson", "hash1", Guid.NewGuid()));
        context.Users.Add(new User(dealerId, "bob@example.com", "Bob", "Brown", "hash2", Guid.NewGuid()));
        context.Users.Add(new User(dealerId, "carol@example.com", "Carol", "Anderson", "hash3", Guid.NewGuid()));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);
        var query = new GetAllUsersQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().HaveCount(3);
        result.Value.Total.Should().Be(3);
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsEmptyList()
    {
        using var context = CreateContext();
        var handler = CreateHandler(context);
        var query = new GetAllUsersQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().BeEmpty();
        result.Value.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OrdersByLastNameFirstName()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        context.Users.Add(new User(dealerId, "alice@example.com", "Alice", "Johnson", "hash1", Guid.NewGuid()));
        context.Users.Add(new User(dealerId, "bob@example.com", "Bob", "Brown", "hash2", Guid.NewGuid()));
        context.Users.Add(new User(dealerId, "carol@example.com", "Carol", "Anderson", "hash3", Guid.NewGuid()));
        context.Users.Add(new User(dealerId, "dave@example.com", "Dave", "Williams", "hash4", Guid.NewGuid()));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);
        var query = new GetAllUsersQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var users = result.Value.Data.ToList();

        // Should be ordered by LastName, then FirstName
        // Anderson, Brown, Johnson, Williams
        users[0].LastName.Should().Be("Anderson");
        users[1].LastName.Should().Be("Brown");
        users[2].LastName.Should().Be("Johnson");
        users[3].LastName.Should().Be("Williams");
    }

    [Fact]
    public async Task Handle_ReturnsCorrectUserData()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var user = new User(dealerId, "test@example.com", "John", "Doe", "hash", Guid.NewGuid());
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);
        var query = new GetAllUsersQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var users = result.Value.Data.ToList();
        var foundUser = users.First(u => u.Email == "test@example.com");
        foundUser.FirstName.Should().Be("John");
        foundUser.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task Handle_TenantIsolation_ReturnsOnlyCurrentTenantUsers()
    {
        using var context = CreateContext();
        var dealerId1 = Guid.NewGuid();
        var dealerId2 = Guid.NewGuid();

        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId1);

        // Add users for dealer1
        context.Users.Add(new User(dealerId1, "dealer1@example.com", "Dealer", "One", "hash1", Guid.NewGuid()));
        // Add users for dealer2
        context.Users.Add(new User(dealerId2, "dealer2@example.com", "Dealer", "Two", "hash2", Guid.NewGuid()));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);
        var query = new GetAllUsersQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data.First().Email.Should().Be("dealer1@example.com");
    }

    [Fact]
    public async Task Handle_ActiveUsersOnly_ReturnsOnlyActive()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        context.Users.Add(new User(dealerId, "active@example.com", "Active", "User", "hash1", Guid.NewGuid()));
        var inactiveUser = new User(dealerId, "inactive@example.com", "Inactive", "User", "hash2", Guid.NewGuid());
        inactiveUser.Deactivate();
        context.Users.Add(inactiveUser);
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);
        var query = new GetAllUsersQuery() { IsActive = true };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data.First().Email.Should().Be("active@example.com");
    }

    [Fact]
    public async Task Handle_SearchFilter_FiltersByName()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        context.Users.Add(new User(dealerId, "john@example.com", "John", "Doe", "hash1", Guid.NewGuid()));
        context.Users.Add(new User(dealerId, "jane@example.com", "Jane", "Smith", "hash2", Guid.NewGuid()));
        context.Users.Add(new User(dealerId, "bob@example.com", "Bob", "Brown", "hash3", Guid.NewGuid()));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);
        var query = new GetAllUsersQuery() { Search = "john" };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data.First().FirstName.Should().Be("John");
    }

    [Fact]
    public async Task Handle_RoleFilter_FiltersByRole()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var mockTenantService = new Mock<ICurrentTenantService>();
        mockTenantService.Setup(x => x.DealerId).Returns(dealerId);

        var roleId = Guid.NewGuid();
        context.Users.Add(new User(dealerId, "admin@example.com", "Admin", "User", "hash1", roleId));
        context.Users.Add(new User(dealerId, "empleado@example.com", "Empleado", "User", "hash2", Guid.NewGuid()));
        context.Users.Add(new User(dealerId, "cliente@example.com", "Cliente", "User", "hash3", Guid.NewGuid()));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, mockTenantService.Object);
        var query = new GetAllUsersQuery() { RoleId = roleId };

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data.First().Role.Should().Be(roleId.ToString());
    }
}