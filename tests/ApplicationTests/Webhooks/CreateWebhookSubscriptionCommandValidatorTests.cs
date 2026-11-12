using Application.Abstractions.Webhooks;
using Application.Webhooks.Create;
using Domain.Webhooks;
using FluentAssertions;
using Moq;
using Xunit;

namespace Application.UnitTests.Webhooks;

public class CreateWebhookSubscriptionCommandValidatorTests
{
    private readonly Mock<IWebhookDestinationGuard> _guardMock;
    private readonly CreateWebhookSubscriptionCommandValidator _validator;

    public CreateWebhookSubscriptionCommandValidatorTests()
    {
        _guardMock = new Mock<IWebhookDestinationGuard>();
        _guardMock.Setup(g => g.ValidateDestinationAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _guardMock.Setup(g => g.ValidateDestinationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _validator = new CreateWebhookSubscriptionCommandValidator(_guardMock.Object);
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenUrlIsPublicAndAllowed()
    {
        var command = new CreateWebhookSubscriptionCommand(
            "https://api.example.com/webhooks",
            [WebhookEventCatalog.SaleCreated]);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://127.0.0.1/webhook")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://10.0.0.5/hook")]
    [InlineData("http://localhost/hook")]
    public async Task Validate_ShouldFail_WhenDestinationGuardRejectsUrl(string blockedUrl)
    {
        _guardMock.Setup(g => g.ValidateDestinationAsync(It.Is<Uri>(u => u.OriginalString == blockedUrl || u.AbsoluteUri == blockedUrl), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _guardMock.Setup(g => g.ValidateDestinationAsync(blockedUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateWebhookSubscriptionCommand(blockedUrl, [WebhookEventCatalog.SaleCreated]);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateWebhookSubscriptionCommand.Url) &&
                                           e.ErrorMessage.Contains("allowed"));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenUrlIsNotAbsoluteHttp()
    {
        var command = new CreateWebhookSubscriptionCommand(
            "ftp://example.com/webhook",
            [WebhookEventCatalog.SaleCreated]);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateWebhookSubscriptionCommand.Url));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenEventTypesIsEmpty()
    {
        var command = new CreateWebhookSubscriptionCommand(
            "https://api.example.com/webhooks",
            []);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateWebhookSubscriptionCommand.EventTypes));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenEventTypeIsUnknown()
    {
        var command = new CreateWebhookSubscriptionCommand(
            "https://api.example.com/webhooks",
            ["invalid.event.type"]);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Unknown webhook event type"));
    }
}
