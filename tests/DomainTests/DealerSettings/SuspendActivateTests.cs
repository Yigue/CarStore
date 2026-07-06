using Domain.DealerSettings;
using Domain.DealerSettings.Events;

namespace DomainTests.DealerSettings;

public class SuspendActivateTests
{
    private static Domain.DealerSettings.DealerSettings CreateDealer()
        => new(
            dealerId: Guid.NewGuid(),
            dealerName: "Test Dealer",
            contactEmail: "test@dealer.com");

    // ---- Suspend happy path -------------------------------------------

    [Fact]
    public void Suspend_ActiveDealer_SetsIsActiveFalse()
    {
        var dealer = CreateDealer();
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        dealer.Suspend("Non-payment", actorId, now);

        dealer.IsActive.Should().BeFalse();
        dealer.SuspendedAt.Should().Be(now);
        dealer.SuspendReason.Should().Be("Non-payment");
    }

    [Fact]
    public void Suspend_RaisesDealerSuspendedDomainEvent()
    {
        var dealer = CreateDealer();
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        dealer.Suspend("Non-payment", actorId, now);

        var ev = dealer.DomainEvents
            .OfType<DealerSuspendedDomainEvent>()
            .Should().ContainSingle().Subject;

        ev.DealerId.Should().Be(dealer.Id);
        ev.Reason.Should().Be("Non-payment");
        ev.ActorId.Should().Be(actorId);
        ev.SuspendedAtUtc.Should().Be(now);
    }

    // ---- Suspend empty reason rejection ---------------------------------

    [Fact]
    public void Suspend_EmptyReason_ThrowsDomainException()
    {
        var dealer = CreateDealer();

        var act = () => dealer.Suspend("", Guid.NewGuid(), DateTime.UtcNow);

        act.Should().Throw<SharedKernel.DomainException>()
            .WithMessage("*SuspendReason*");
    }

    [Fact]
    public void Suspend_WhitespaceReason_ThrowsDomainException()
    {
        var dealer = CreateDealer();

        var act = () => dealer.Suspend("   ", Guid.NewGuid(), DateTime.UtcNow);

        act.Should().Throw<SharedKernel.DomainException>();
    }

    // ---- Suspend idempotent ---------------------------------------------

    [Fact]
    public void Suspend_AlreadySuspended_IsIdempotent_NoExtraEvent()
    {
        var dealer = CreateDealer();
        var now = DateTime.UtcNow;
        dealer.Suspend("First", Guid.NewGuid(), now);
        dealer.ClearDomainEvents();

        // Second suspend with same or different reason — idempotent, no event
        dealer.Suspend("Second", Guid.NewGuid(), now.AddMinutes(1));

        dealer.IsActive.Should().BeFalse();
        dealer.SuspendReason.Should().Be("First"); // unchanged
        dealer.DomainEvents.Should().BeEmpty();
    }

    // ---- Activate happy path --------------------------------------------

    [Fact]
    public void Activate_SuspendedDealer_SetsIsActiveTrue()
    {
        var dealer = CreateDealer();
        dealer.Suspend("reason", Guid.NewGuid(), DateTime.UtcNow);
        dealer.ClearDomainEvents();

        dealer.Activate();

        dealer.IsActive.Should().BeTrue();
        dealer.SuspendedAt.Should().BeNull();
        dealer.SuspendReason.Should().BeNull();
    }

    [Fact]
    public void Activate_RaisesDealerReactivatedDomainEvent()
    {
        var dealer = CreateDealer();
        dealer.Suspend("reason", Guid.NewGuid(), DateTime.UtcNow);
        dealer.ClearDomainEvents();

        dealer.Activate();

        dealer.DomainEvents
            .OfType<DealerReactivatedDomainEvent>()
            .Should().ContainSingle()
            .Which.DealerId.Should().Be(dealer.Id);
    }

    // ---- Activate idempotent --------------------------------------------

    [Fact]
    public void Activate_AlreadyActive_IsIdempotent_NoEvent()
    {
        var dealer = CreateDealer();

        dealer.Activate(); // already active

        dealer.IsActive.Should().BeTrue();
        dealer.DomainEvents.OfType<DealerReactivatedDomainEvent>().Should().BeEmpty();
    }

    // ---- DealerSettingsErrors -------------------------------------------

    [Fact]
    public void DealerSettingsErrors_SuspendReasonRequired_HasExpectedCode()
    {
        DealerSettingsErrors.SuspendReasonRequired.Code
            .Should().Contain("SuspendReasonRequired");
    }

    [Fact]
    public void DealerSettingsErrors_AlreadySuspended_HasExpectedCode()
    {
        DealerSettingsErrors.AlreadySuspended.Code
            .Should().Contain("AlreadySuspended");
    }

    [Fact]
    public void DealerSettingsErrors_NotSuspended_HasExpectedCode()
    {
        DealerSettingsErrors.NotSuspended.Code
            .Should().Contain("NotSuspended");
    }
}
