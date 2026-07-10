using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Clients.Create;
using Application.Abstractions.Tenancy;
using Domain.Clients.Attributes;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests.Clients;

/// <summary>
/// PR1 tests for CreateClientCommandHandler — verifies Type is persisted and DealerId is set.
/// </summary>
public class CreateClientCommandHandlerTests
{
    private static readonly Guid TestDealerId = Guid.Parse("aaaabbbb-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static (TestApplicationDbContext context, ICurrentTenantService tenantService) CreateSut()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new TestApplicationDbContext(options);

        var mockTenant = new Mock<ICurrentTenantService>();
        mockTenant.Setup(t => t.DealerId).Returns(TestDealerId);
        mockTenant.Setup(t => t.HasTenant).Returns(true);

        return (context, mockTenant.Object);
    }

    [Fact]
    public async Task Handle_CreatesClient_WithIndividualType()
    {
        var (context, tenantService) = CreateSut();
        using (context)
        {
            var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc) };
            var handler = new CreateClientCommandHandler(context, dateProvider, tenantService);
            var command = new CreateClientCommand("Ana", "Perez", "12345678", "ana@test.com", "111", "Calle 1", ClientType.Individual);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var clientId = result.Value;
            var persisted = await context.Clients.FindAsync(clientId);
            persisted.Should().NotBeNull();
            persisted!.FirstName.Should().Be("Ana");
            persisted.Type.Should().Be(ClientType.Individual);
            persisted.DealerId.Should().Be(TestDealerId);
        }
    }

    [Fact]
    public async Task Handle_CreatesClient_WithCorporateType()
    {
        var (context, tenantService) = CreateSut();
        using (context)
        {
            var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc) };
            var handler = new CreateClientCommandHandler(context, dateProvider, tenantService);
            var command = new CreateClientCommand("ACME", "S.A.", "30123456", "acme@test.com", "222", "Av Industrial 1", ClientType.Corporate);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var persisted = await context.Clients.FindAsync(result.Value);
            persisted!.Type.Should().Be(ClientType.Corporate, "Corporate type must be persisted, not defaulted to Individual");
        }
    }

    [Fact]
    public async Task Handle_ReturnsClientId_AsNonEmptyGuid()
    {
        var (context, tenantService) = CreateSut();
        using (context)
        {
            var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc) };
            var handler = new CreateClientCommandHandler(context, dateProvider, tenantService);
            var command = new CreateClientCommand("Juan", "Lopez", "99887766", "juan@test.com", "333", "Calle 2", ClientType.Individual);

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeEmpty();
        }
    }

    [Fact]
    public async Task Handle_PersistsOptionalFields_WhenProvided()
    {
        var (context, tenantService) = CreateSut();
        using (context)
        {
            var dateProvider = new FakeDateTimeProvider { UtcNow = new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc) };
            var handler = new CreateClientCommandHandler(context, dateProvider, tenantService);
            var command = new CreateClientCommand(
                "Pedro", "Gil", "55443322", "pedro@test.com", "444", "Calle 3",
                ClientType.Individual, City: "Buenos Aires", ZipCode: "1001", Notes: "Test notes");

            var result = await handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            var persisted = await context.Clients.FindAsync(result.Value);
            persisted!.City.Should().Be("Buenos Aires");
            persisted.ZipCode.Should().Be("1001");
            persisted.Notes.Should().Be("Test notes");
        }
    }
}
