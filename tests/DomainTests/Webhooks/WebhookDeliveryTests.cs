using Domain.Webhooks;

namespace DomainTests.Webhooks;

public class WebhookDeliveryTests
{
    private static WebhookDelivery CreateDelivery(DateTime now) => WebhookDelivery.Create(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        WebhookEventCatalog.SaleCreated,
        "{\"event\":\"sale.created\"}",
        now);

    [Fact]
    public void Create_ShouldStartPending_WithZeroAttempts_AndImmediateNextRetry()
    {
        var now = DateTime.UtcNow;
        var delivery = CreateDelivery(now);

        delivery.Status.Should().Be(WebhookDeliveryStatus.Pending);
        delivery.AttemptCount.Should().Be(0);
        delivery.NextRetryAtUtc.Should().Be(now);
    }

    [Fact]
    public void RecordSuccess_ShouldMarkDelivered_AndStampStatusCode()
    {
        var now = DateTime.UtcNow;
        var delivery = CreateDelivery(now);

        delivery.RecordSuccess(now.AddSeconds(1), 200);

        delivery.Status.Should().Be(WebhookDeliveryStatus.Delivered);
        delivery.LastStatusCode.Should().Be(200);
        delivery.DeliveredAtUtc.Should().Be(now.AddSeconds(1));
        delivery.LastError.Should().BeNull();
    }

    [Theory]
    [InlineData(1, 1)]   // attempt 1 -> 1 minute
    [InlineData(2, 5)]   // attempt 2 -> 5 minutes
    [InlineData(3, 30)]  // attempt 3 -> 30 minutes
    [InlineData(4, 120)] // attempt 4 -> 2 hours (120 minutes)
    public void RecordFailure_ShouldScheduleBackoff_PerRetryPolicy(int attemptsToReach, int expectedMinutes)
    {
        var now = DateTime.UtcNow;
        var delivery = CreateDelivery(now);

        for (int i = 0; i < attemptsToReach; i++)
        {
            delivery.RecordFailure(now, statusCode: 500, "boom");
        }

        delivery.AttemptCount.Should().Be(attemptsToReach);
        delivery.Status.Should().Be(WebhookDeliveryStatus.Pending);
        delivery.NextRetryAtUtc.Should().Be(now.AddMinutes(expectedMinutes));
    }

    [Fact]
    public void RecordFailure_ShouldDeadLetter_AfterMaxAttempts()
    {
        var now = DateTime.UtcNow;
        var delivery = CreateDelivery(now);

        for (int i = 0; i < WebhookRetryPolicy.MaxAttempts; i++)
        {
            delivery.RecordFailure(now, statusCode: 500, "boom");
        }

        delivery.AttemptCount.Should().Be(WebhookRetryPolicy.MaxAttempts);
        delivery.Status.Should().Be(WebhookDeliveryStatus.DeadLettered);
    }

    [Fact]
    public void RecordFailure_ShouldCaptureLastErrorAndStatusCode()
    {
        var now = DateTime.UtcNow;
        var delivery = CreateDelivery(now);

        delivery.RecordFailure(now, 503, "Service Unavailable");

        delivery.LastStatusCode.Should().Be(503);
        delivery.LastError.Should().Be("Service Unavailable");
    }
}
