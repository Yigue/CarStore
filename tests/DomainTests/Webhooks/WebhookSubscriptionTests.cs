using Domain.Webhooks;

namespace DomainTests.Webhooks;

public class WebhookSubscriptionTests
{
    private const string ValidSecret = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void Create_ShouldSucceed_WithValidUrlAndKnownEventTypes()
    {
        var subscription = WebhookSubscription.Create(
            Guid.NewGuid(),
            "https://example.com/hooks/carstore",
            ValidSecret,
            [WebhookEventCatalog.SaleCreated, WebhookEventCatalog.LeadStatusChanged],
            DateTime.UtcNow);

        subscription.IsActive.Should().BeTrue();
        subscription.EventTypes.Should().Contain(WebhookEventCatalog.SaleCreated);
        subscription.EventTypes.Should().Contain(WebhookEventCatalog.LeadStatusChanged);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/hook")]
    public void Create_ShouldThrow_WhenUrlInvalid(string url)
    {
        var act = () => WebhookSubscription.Create(
            Guid.NewGuid(), url, ValidSecret, [WebhookEventCatalog.SaleCreated], DateTime.UtcNow);

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("http://localhost/hook")]
    [InlineData("http://127.0.0.1/hook")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://10.0.0.5/hook")]
    [InlineData("http://172.16.0.5/hook")]
    [InlineData("http://192.168.1.5/hook")]
    [InlineData("http://metadata.google.internal/computeMetadata/v1/")]
    public void Create_ShouldThrow_WhenUrlTargetsPrivateOrLoopbackHost(string url)
    {
        // SSRF guard: the dispatcher makes real server-side POSTs to this URL on a
        // timer, so a dealer-admin must not be able to point it at internal
        // infrastructure or a cloud metadata endpoint.
        var act = () => WebhookSubscription.Create(
            Guid.NewGuid(), url, ValidSecret, [WebhookEventCatalog.SaleCreated], DateTime.UtcNow);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenSecretTooShort()
    {
        var act = () => WebhookSubscription.Create(
            Guid.NewGuid(), "https://example.com", "short", [WebhookEventCatalog.SaleCreated], DateTime.UtcNow);

        act.Should().Throw<DomainException>().WithMessage("*secret*");
    }

    [Fact]
    public void Create_ShouldThrow_WhenEventTypesEmpty()
    {
        var act = () => WebhookSubscription.Create(
            Guid.NewGuid(), "https://example.com", ValidSecret, [], DateTime.UtcNow);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenEventTypeUnknown()
    {
        var act = () => WebhookSubscription.Create(
            Guid.NewGuid(), "https://example.com", ValidSecret, ["not.a.real.event"], DateTime.UtcNow);

        act.Should().Throw<DomainException>().WithMessage("*Unknown webhook event type*");
    }

    [Fact]
    public void IsSubscribedTo_ShouldReturnFalse_WhenInactive()
    {
        var subscription = WebhookSubscription.Create(
            Guid.NewGuid(), "https://example.com", ValidSecret, [WebhookEventCatalog.SaleCreated], DateTime.UtcNow);

        subscription.UpdateDetails("https://example.com", [WebhookEventCatalog.SaleCreated], isActive: false);

        subscription.IsSubscribedTo(WebhookEventCatalog.SaleCreated).Should().BeFalse();
    }

    [Fact]
    public void IsSubscribedTo_ShouldReturnFalse_WhenEventTypeNotInList()
    {
        var subscription = WebhookSubscription.Create(
            Guid.NewGuid(), "https://example.com", ValidSecret, [WebhookEventCatalog.SaleCreated], DateTime.UtcNow);

        subscription.IsSubscribedTo(WebhookEventCatalog.LeadStatusChanged).Should().BeFalse();
    }

    [Fact]
    public void UpdateDetails_ShouldReplaceUrlEventTypesAndActiveFlag()
    {
        var subscription = WebhookSubscription.Create(
            Guid.NewGuid(), "https://example.com", ValidSecret, [WebhookEventCatalog.SaleCreated], DateTime.UtcNow);

        subscription.UpdateDetails(
            "https://new.example.com",
            [WebhookEventCatalog.ClientCreated],
            isActive: false);

        subscription.Url.Should().Be("https://new.example.com");
        subscription.EventTypes.Should().ContainSingle().Which.Should().Be(WebhookEventCatalog.ClientCreated);
        subscription.IsActive.Should().BeFalse();
    }
}
