using System.Threading.Tasks;
using Application.DealerSettings.Commands.UpdateDealerVisual;
using FluentAssertions;
using Xunit;

namespace Application.UnitTests.DealerSettings;

public class UpdateDealerVisualCommandValidatorTests
{
    private readonly UpdateDealerVisualCommandValidator _validator = new();

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("ftp://example.com/logo.png")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not-a-url")]
    [InlineData("/images/logo.png")]
    [InlineData("relative/logo.png")]
    public async Task Validate_WhenLogoUrlIsNotHttpOrHttps_ShouldFailValidation(string invalidUrl)
    {
        // Arrange
        var command = new UpdateDealerVisualCommand(
            invalidUrl,
            "#000000",
            "#ffffff",
            "Footer text");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse($"LogoUrl '{invalidUrl}' should be rejected as non-http(s)");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateDealerVisualCommand.LogoUrl));
    }

    [Theory]
    [InlineData("https://example.com/logo.png")]
    [InlineData("http://cdn.dealers.com/images/logo.svg")]
    [InlineData(null)]
    [InlineData("")]
    public async Task Validate_WhenLogoUrlIsValidOrEmpty_ShouldPassValidation(string? validUrl)
    {
        // Arrange
        var command = new UpdateDealerVisualCommand(
            validUrl,
            "#000000",
            "#ffffff",
            "Footer text");

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
