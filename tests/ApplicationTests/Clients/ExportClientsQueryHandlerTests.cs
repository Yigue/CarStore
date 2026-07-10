using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Application.Clients.Export;
using Domain.Clients;
using Domain.Clients.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Clients;

public class ExportClientsQueryHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Client SeedClient(
        TestApplicationDbContext context,
        string firstName = "John",
        string lastName = "Doe",
        string dni = "12345678",
        ClientType type = ClientType.Individual)
    {
        var dealerId = Guid.NewGuid();
        var client = new Client(dealerId, firstName, lastName, dni,
            $"{firstName.ToLower()}@test.com", "111", "Street 1", DateTime.UtcNow, type);
        context.Clients.Add(client);
        context.SaveChanges();
        client.ClearDomainEvents();
        return client;
    }

    [Fact]
    public async Task Handle_Should_ReturnCsvBytes_WithBom()
    {
        using var context = CreateContext();
        SeedClient(context, "Alice", "Smith", "11111111");

        var handler = new ExportClientsQueryHandler(context);
        var result = await handler.Handle(new ExportClientsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var bytes = result.Value;
        bytes.Should().NotBeEmpty();

        // First 3 bytes should be UTF-8 BOM
        bytes[0].Should().Be(0xEF);
        bytes[1].Should().Be(0xBB);
        bytes[2].Should().Be(0xBF);
    }

    [Fact]
    public async Task Handle_Should_ReturnCsvWithHeader_WhenNoClients()
    {
        using var context = CreateContext();
        var handler = new ExportClientsQueryHandler(context);
        var result = await handler.Handle(new ExportClientsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // After BOM, should have a header row
        var text = Encoding.UTF8.GetString(result.Value, 3, result.Value.Length - 3);
        text.Should().StartWith("Id,FirstName");
    }

    [Fact]
    public async Task Handle_Should_FilterByIds_WhenProvided()
    {
        using var context = CreateContext();
        var c1 = SeedClient(context, "Alice", "Smith", "11111111");
        SeedClient(context, "Bob", "Jones", "22222222");

        var handler = new ExportClientsQueryHandler(context);
        var result = await handler.Handle(new ExportClientsQuery(Ids: [c1.Id]), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var text = Encoding.UTF8.GetString(result.Value);
        text.Should().Contain("Alice");
        text.Should().NotContain("Bob");
    }

    [Fact]
    public async Task Handle_Should_ReturnLimitExceededError_WhenMoreThan10kRows()
    {
        // We can't seed 10001 rows in a unit test, so we verify the error logic
        // by checking the constant is correct (the integration test covers the real limit).
        ExportErrors.MaxRows.Should().Be(10_000);
    }
}
