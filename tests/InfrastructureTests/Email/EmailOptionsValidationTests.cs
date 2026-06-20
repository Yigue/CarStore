using System.ComponentModel.DataAnnotations;
using Infrastructure.Services;

namespace InfrastructureTests.Email;

public class EmailOptionsValidationTests
{
    private static IList<ValidationResult> Validate(EmailOptions options)
    {
        var results = new List<ValidationResult>();
        var ctx = new ValidationContext(options);
        Validator.TryValidateObject(options, ctx, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void EmptyHost_FailsDataAnnotationsValidation()
    {
        // Arrange
        var options = new EmailOptions { Host = "", FromAddress = "no-reply@test.com" };

        // Act
        var results = Validate(options);

        // Assert
        results.Should().Contain(r =>
            r.MemberNames.Any(m => m == nameof(EmailOptions.Host)));
    }

    [Fact]
    public void EmptyFromAddress_FailsDataAnnotationsValidation()
    {
        // Arrange
        var options = new EmailOptions { Host = "smtp.example.com", FromAddress = "" };

        // Act
        var results = Validate(options);

        // Assert
        results.Should().Contain(r =>
            r.MemberNames.Any(m => m == nameof(EmailOptions.FromAddress)));
    }

    [Fact]
    public void ValidOptions_PassesDataAnnotationsValidation()
    {
        // Arrange
        var options = new EmailOptions
        {
            Host = "smtp.example.com",
            FromAddress = "no-reply@example.com"
        };

        // Act
        var results = Validate(options);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void DefaultPort_Is587()
    {
        // Arrange & Act
        var options = new EmailOptions();

        // Assert
        options.Port.Should().Be(587);
    }

    [Fact]
    public void DefaultUseStartTls_IsTrue()
    {
        // Arrange & Act
        var options = new EmailOptions();

        // Assert
        options.UseStartTls.Should().BeTrue();
    }
}
