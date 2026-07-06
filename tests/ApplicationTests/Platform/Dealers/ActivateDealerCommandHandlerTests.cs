using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Platform.Dealers.ActivateDealer;
using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Platform.Dealers;

public class ActivateDealerCommandHandlerTests
{
    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestApplicationDbContext(options);
    }

    private static Domain.DealerSettings.DealerSettings SeedDealer(
        TestApplicationDbContext context,
        bool isActive = true)
    {
        var ds = new Domain.DealerSettings.DealerSettings(Guid.NewGuid(), "Test Dealer", "test@dealer.com");
        if (!isActive)
            ds.Suspend("reason", Guid.NewGuid(), DateTime.UtcNow);

        context.DealerSettings.Add(ds);
        context.SaveChanges();
        ds.ClearDomainEvents();
        return ds;
    }

    [Fact]
    public async Task Handle_SuspendedDealer_ReactivatesDealer()
    {
        using var context = CreateContext();
        var dealer = SeedDealer(context, isActive: false);

        var command = new ActivateDealerCommand(dealer.Id, "v0");
        var handler = new ActivateDealerCommandHandler(context);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        result.Value.SuspendedAt.Should().BeNull();
        result.Value.SuspendReason.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AlreadyActive_IsIdempotent()
    {
        using var context = CreateContext();
        var dealer = SeedDealer(context, isActive: true);

        var command = new ActivateDealerCommand(dealer.Id, "v0");
        var handler = new ActivateDealerCommandHandler(context);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_StaleETag_Returns412()
    {
        using var context = CreateContext();
        var dealer = SeedDealer(context, isActive: false);

        var command = new ActivateDealerCommand(dealer.Id, "v999");
        var handler = new ActivateDealerCommandHandler(context);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("ETagMismatch");
    }

    [Fact]
    public async Task Handle_MissingDealer_ReturnsNotFound()
    {
        using var context = CreateContext();

        var command = new ActivateDealerCommand(Guid.NewGuid(), "v0");
        var handler = new ActivateDealerCommandHandler(context);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(SharedKernel.ErrorType.NotFound);
    }
}
