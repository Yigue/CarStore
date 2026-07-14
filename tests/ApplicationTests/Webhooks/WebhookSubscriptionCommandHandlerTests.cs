using Application.Abstractions.Tenancy;
using Application.UnitTests;
using Application.Webhooks.Create;
using Application.Webhooks.Delete;
using Application.Webhooks.GetAll;
using Application.Webhooks.Update;
using Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Moq;
using SharedKernel;

namespace Application.UnitTests.Webhooks;

/// <summary>CRUD handler coverage for tenant-scoped webhook subscriptions.</summary>
public class WebhookSubscriptionCommandHandlerTests
{
    private static readonly Guid DealerId = Guid.NewGuid();

    private TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestApplicationDbContext(options);
    }

    private static ICurrentTenantService TenantService() =>
        Mock.Of<ICurrentTenantService>(t => t.DealerId == DealerId && t.HasTenant == true);

    [Fact]
    public async Task Create_ShouldGenerateSecret_AndPersistSubscription()
    {
        var context = CreateContext();
        var handler = new CreateWebhookSubscriptionCommandHandler(context, TenantService(), new FakeDateTimeProvider());

        var result = await handler.Handle(
            new CreateWebhookSubscriptionCommand("https://example.com/hook", [WebhookEventCatalog.SaleCreated]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Secret.Should().NotBeNullOrWhiteSpace();
        result.Value.Secret.Length.Should().BeGreaterThanOrEqualTo(32);

        var stored = await context.WebhookSubscriptions.FirstAsync();
        stored.DealerId.Should().Be(DealerId);
        stored.Secret.Should().Be(result.Value.Secret);
    }

    [Fact]
    public async Task Create_ShouldReturnValidationFailure_ForUnknownEventType()
    {
        var context = CreateContext();
        var handler = new CreateWebhookSubscriptionCommandHandler(context, TenantService(), new FakeDateTimeProvider());

        var result = await handler.Handle(
            new CreateWebhookSubscriptionCommand("https://example.com/hook", ["not.a.real.event"]),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task GetAll_ShouldMaskSecret()
    {
        var context = CreateContext();
        var createHandler = new CreateWebhookSubscriptionCommandHandler(context, TenantService(), new FakeDateTimeProvider());
        var created = await createHandler.Handle(
            new CreateWebhookSubscriptionCommand("https://example.com/hook", [WebhookEventCatalog.SaleCreated]),
            CancellationToken.None);

        var getAllHandler = new GetWebhookSubscriptionsQueryHandler(context);
        var result = await getAllHandler.Handle(new GetWebhookSubscriptionsQuery(), CancellationToken.None);

        result.Value.Should().ContainSingle();
        result.Value[0].MaskedSecret.Should().NotBe(created.Value.Secret);
        result.Value[0].MaskedSecret.Should().EndWith(created.Value.Secret[^4..]);
    }

    [Fact]
    public async Task Update_ShouldReturnNotFound_ForUnknownId()
    {
        var context = CreateContext();
        var handler = new UpdateWebhookSubscriptionCommandHandler(context);
        var missingId = Guid.NewGuid();

        var result = await handler.Handle(
            new UpdateWebhookSubscriptionCommand(missingId, "https://example.com", [WebhookEventCatalog.SaleCreated], true),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(WebhookErrors.NotFound(missingId));
    }

    [Fact]
    public async Task Update_ShouldModifySubscription_WhenFound()
    {
        var context = CreateContext();
        var subscription = WebhookSubscription.Create(
            DealerId, "https://old.example.com", "0123456789abcdef0123456789abcdef",
            [WebhookEventCatalog.SaleCreated], DateTime.UtcNow);
        context.WebhookSubscriptions.Add(subscription);
        context.SaveChanges();

        var handler = new UpdateWebhookSubscriptionCommandHandler(context);
        var result = await handler.Handle(
            new UpdateWebhookSubscriptionCommand(subscription.Id, "https://new.example.com", [WebhookEventCatalog.ClientCreated], false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var updated = await context.WebhookSubscriptions.FirstAsync(s => s.Id == subscription.Id);
        updated.Url.Should().Be("https://new.example.com");
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ShouldRemoveSubscription_WhenFound()
    {
        var context = CreateContext();
        var subscription = WebhookSubscription.Create(
            DealerId, "https://example.com", "0123456789abcdef0123456789abcdef",
            [WebhookEventCatalog.SaleCreated], DateTime.UtcNow);
        context.WebhookSubscriptions.Add(subscription);
        context.SaveChanges();

        var handler = new DeleteWebhookSubscriptionCommandHandler(context);
        var result = await handler.Handle(new DeleteWebhookSubscriptionCommand(subscription.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await context.WebhookSubscriptions.AnyAsync(s => s.Id == subscription.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ShouldReturnNotFound_ForUnknownId()
    {
        var context = CreateContext();
        var handler = new DeleteWebhookSubscriptionCommandHandler(context);

        var result = await handler.Handle(new DeleteWebhookSubscriptionCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
