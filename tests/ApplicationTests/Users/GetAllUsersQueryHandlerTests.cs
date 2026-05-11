using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Application.Queries.Users.GetAll;
using Domain.Users;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Users;

public class GetAllUsersQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static async Task SeedTestDataAsync(TestApplicationDbContext context)
    {
        var dealerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Create users with different last names
        context.Users.Add(new User(dealerId, "alice@test.com", "Alice", "Johnson", "hash1"));
        context.Users.Add(new User(dealerId, "bob@test.com", "Bob", "Brown", "hash2"));
        context.Users.Add(new User(dealerId, "carol@test.com", "Carol", "Anderson", "hash3"));
        context.Users.Add(new User(dealerId, "dave@test.com", "Dave", "Williams", "hash4"));

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_ReturnsAllUsers()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetAllUsersQueryHandler(context);
        var query = new GetAllUsersQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(4);
    }

    [Fact]
    public async Task Handle_OrdersByLastNameFirstName()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetAllUsersQueryHandler(context);
        var query = new GetAllUsersQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var users = result.Value.ToList();

        // Should be ordered by LastName, then FirstName
        // Anderson, Johnson, Brown, Williams (alphabetical by last name)
        users[0].LastName.Should().Be("Anderson");
        users[1].LastName.Should().Be("Brown");
        users[2].LastName.Should().Be("Johnson");
        users[3].LastName.Should().Be("Williams");
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoUsers()
    {
        using var context = CreateContext();
        var handler = new GetAllUsersQueryHandler(context);
        var query = new GetAllUsersQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsCorrectUserData()
    {
        using var context = CreateContext();
        await SeedTestDataAsync(context);
        var handler = new GetAllUsersQueryHandler(context);
        var query = new GetAllUsersQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var users = result.Value.ToList();

        var alice = users.First(u => u.FirstName == "Alice");
        alice.Email.Should().Be("alice@test.com");
        alice.LastName.Should().Be("Johnson");
    }

    [Fact]
    public async Task Handle_OrdersByLastNameWithinSameLastName()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();

        // Create users with same last name, different first names
        context.Users.Add(new User(dealerId, "a@test.com", "Aaron", "Smith", "hash1"));
        context.Users.Add(new User(dealerId, "b@test.com", "Barbara", "Smith", "hash2"));
        context.Users.Add(new User(dealerId, "c@test.com", "Brian", "Smith", "hash3"));
        await context.SaveChangesAsync();

        var handler = new GetAllUsersQueryHandler(context);
        var query = new GetAllUsersQuery();

        var result = await handler.Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var users = result.Value.ToList();

        // Within same last name, should be ordered by first name
        // Aaron, Barbara, Brian
        users[0].FirstName.Should().Be("Aaron");
        users[1].FirstName.Should().Be("Barbara");
        users[2].FirstName.Should().Be("Brian");
    }
}