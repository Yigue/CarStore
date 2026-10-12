using Domain.Billing;

namespace DomainTests.Billing;

/// <summary>sfc A2.1.4 / RN-023: the aggregate remembers the newest applied provider event.</summary>
public class DealerSubscriptionProviderEventTests
{
    private static readonly DateTime T1 = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    private static DealerSubscription NewSubscription() =>
        DealerSubscription.Create(Guid.NewGuid(), "cus_1", "sub_1", "price_1");

    [Fact]
    public void NewSubscription_HasNoProviderEventReference()
    {
        NewSubscription().LastProviderEventAtUtc.Should().BeNull();
    }

    [Fact]
    public void TryRegisterProviderEvent_FirstEvent_IsAcceptedAndStored()
    {
        var sub = NewSubscription();

        sub.TryRegisterProviderEvent(T1).Should().BeTrue();

        sub.LastProviderEventAtUtc.Should().Be(T1);
    }

    [Fact]
    public void TryRegisterProviderEvent_NewerEvent_AdvancesReference()
    {
        var sub = NewSubscription();
        sub.TryRegisterProviderEvent(T1);

        sub.TryRegisterProviderEvent(T1.AddSeconds(1)).Should().BeTrue();

        sub.LastProviderEventAtUtc.Should().Be(T1.AddSeconds(1));
    }

    [Fact]
    public void TryRegisterProviderEvent_SameTimestamp_IsAccepted()
    {
        var sub = NewSubscription();
        sub.TryRegisterProviderEvent(T1);

        sub.TryRegisterProviderEvent(T1).Should().BeTrue();
    }

    [Fact]
    public void TryRegisterProviderEvent_OlderEvent_IsRejectedAndKeepsReference()
    {
        var sub = NewSubscription();
        sub.TryRegisterProviderEvent(T1);

        sub.TryRegisterProviderEvent(T1.AddSeconds(-1)).Should().BeFalse();

        sub.LastProviderEventAtUtc.Should().Be(T1);
    }
}
