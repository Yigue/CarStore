using Application.Abstractions.Webhooks;
using Application.Webhooks.Update;
using Domain.Webhooks;
using FluentAssertions;
using Moq;
using Xunit;

namespace Application.UnitTests.Webhooks;

public class UpdateWebhookSubscriptionCommandValidatorTests
{
    private readonly Mock<IWebhookDestinationGuard> _guardMock;
    private readonly UpdateWebhookSubscriptionCommandValidator _validator;

    public UpdateWebhookSubscriptionCommandValidatorTests()
    {
        _guardMock = new Mock<IWebhookDestinationGuard>();
        _guardMock.Setup(g => g.ValidateDestinationAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _guardMock.Setup(g => g.ValidateDestinationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _validator = new UpdateWebhookSubscriptionCommandValidator(_guardMock.Object);
    }

    [Fact]
    public async Task Validate_ShouldPass_WhenUrlIsPublicAndAllowed()
    {
        var command = new UpdateWebhookSubscriptionCommand(
            Guid.NewGuid(),
            "https://api.example.com/webhooks-updated",
            [WebhookEventCatalog.SaleCreated],
            true);

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

        var command = new UpdateWebhookSubscriptionCommand(
            Guid.NewGuid(),
            blockedUrl,
            [WebhookEventCatalog.SaleCreated],
            true);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateWebhookSubscriptionCommand.Url) &&
                                           e.ErrorMessage.Contains("allowed"));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenIdIsEmpty()
    {
        var command = new UpdateWebhookSubscriptionCommand(
            Guid.Empty,
            "https://api.example.com/webhooks",
            [WebhookEventCatalog.SaleCreated],
            true);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateWebhookSubscriptionCommand.Id));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenUrlIsNotAbsoluteHttp()
    {
        var command = new UpdateWebhookSubscriptionCommand(
            Guid.NewGuid(),
            "ftp://example.com/webhook",
            [WebhookEventCatalog.SaleCreated],
            true);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateWebhookSubscriptionCommand.Url));
    }

    [Fact]
    public async Task Validate_ShouldFail_WhenEventTypesIsEmpty()
    {
        var command = new UpdateWebhookSubscriptionCommand(
            Guid.NewGuid(),
            "https://api.example.com/webhooks",
            [],
            true);

        var result = await _validator.ValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateWebhookSubscriptionCommand.EventTypes));
    }
}
