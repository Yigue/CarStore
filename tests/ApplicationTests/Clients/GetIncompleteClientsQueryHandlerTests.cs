using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Queries.Clients.GetIncomplete;
using Domain.Clients;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Clients;

public class GetIncompleteClientsQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyClientsWithPlaceholderDni()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // 3 incomplete clients: placeholder DNI is "n" + Guid("N") = 33 chars
        for (int i = 0; i < 3; i++)
        {
            var placeholderDni = "n" + Guid.NewGuid().ToString("N");
            context.Clients.Add(new Client(dealerId, $"Inc{i}", $"Last{i}", placeholderDni, $"inc{i}@test.com", "111", "Addr", now));
        }

        // 10 complete clients with real DNIs
        for (int i = 0; i < 10; i++)
        {
            context.Clients.Add(new Client(dealerId, $"Ok{i}", $"Last{i}", $"3011122{i}", $"ok{i}@test.com", "111", "Addr", now));
        }

        await context.SaveChangesAsync();

        var handler = new GetIncompleteClientsQueryHandler(context);

        var result = await handler.Handle(new GetIncompleteClientsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Should().OnlyContain(c => c.FirstName.StartsWith("Inc"));
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoIncompleteClients()
    {
        using var context = CreateContext();
        var dealerId = Guid.NewGuid();
        context.Clients.Add(new Client(dealerId, "Ok", "Last", "30111222", "ok@test.com", "111", "Addr", DateTime.UtcNow));
        await context.SaveChangesAsync();

        var handler = new GetIncompleteClientsQueryHandler(context);

        var result = await handler.Handle(new GetIncompleteClientsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
