using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Platform.Dealers.SuspendDealer;
using Application.Platform.Common;
using Domain.DealerSettings;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace Application.UnitTests.Platform.Dealers;

public class SuspendDealerCommandHandlerTests
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
        var ds = new Domain.DealerSettings.DealerSettings(
            Guid.NewGuid(),
            "Test Dealer",
            "test@dealer.com");

        if (!isActive)
            ds.Suspend("Initial suspension", Guid.NewGuid(), DateTime.UtcNow);

        context.DealerSettings.Add(ds);
        context.SaveChanges();
        ds.ClearDomainEvents();
        return ds;
    }

    [Fact]
    public async Task Handle_ValidReason_SuspendsDealer()
    {
        using var context = CreateContext();
        var dealer = SeedDealer(context);

        var command = new SuspendDealerCommand(dealer.Id, "Non-payment", "v0", Guid.NewGuid());
        var handler = new SuspendDealerCommandHandler(context);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        result.Value.SuspendReason.Should().Be("Non-payment");
    }

    [Fact]
    public async Task Handle_EmptyReason_ReturnsBadRequest()
    {
        using var context = CreateContext();
        var dealer = SeedDealer(context);

        var command = new SuspendDealerCommand(dealer.Id, "", "v0", Guid.NewGuid());
        var handler = new SuspendDealerCommandHandler(context);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("SuspendReasonRequired");
    }

    [Fact]
    public async Task Handle_MissingDealer_ReturnsNotFound()
    {
        using var context = CreateContext();

        var command = new SuspendDealerCommand(Guid.NewGuid(), "reason", "v0", Guid.NewGuid());
        var handler = new SuspendDealerCommandHandler(context);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(SharedKernel.ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_StaleETag_Returns412ETagMismatch()
    {
        using var context = CreateContext();
        var dealer = SeedDealer(context);
        // RowVersion = 0 in InMemory, so ETag = "v0"
        // Send "v1" which doesn't match "v0" → 412

        var command = new SuspendDealerCommand(dealer.Id, "reason", "v1", Guid.NewGuid());
        var handler = new SuspendDealerCommandHandler(context);

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("ETagMismatch");
    }

    [Fact]
    public async Task Handle_AlreadySuspended_IsIdempotent()
    {
        using var context = CreateContext();
        var dealer = SeedDealer(context, isActive: false);

        var command = new SuspendDealerCommand(dealer.Id, "Second reason", "v0", Guid.NewGuid());
        var handler = new SuspendDealerCommandHandler(context);

        var result = await handler.Handle(command, CancellationToken.None);

        // Idempotent: already suspended, returns success
        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
    }
}
