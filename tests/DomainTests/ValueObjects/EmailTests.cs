using Domain.Shared.ValueObjects;
using SharedKernel;

namespace DomainTests.ValueObjects;

public class EmailTests
{
    [Fact]
    public void Constructor_WithValidEmail_ShouldCreateEmail()
    {
        var emailString = "test@example.com";
        var email = new Email(emailString);

        email.Value.Should().Be(emailString.ToLowerInvariant().Trim());
    }

    [Fact]
    public void Constructor_WithEmailWithSpaces_ShouldTrim()
    {
        var emailString = "  test@example.com  ";
        var email = new Email(emailString);

        email.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void Constructor_WithEmailWithUpperCase_ShouldConvertToLower()
    {
        var emailString = "TEST@EXAMPLE.COM";
        var email = new Email(emailString);

        email.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void Constructor_WithEmptyEmail_ShouldThrowDomainException()
    {
        var action = () => new Email("");

        action.Should().Throw<DomainException>()
            .WithMessage("Email cannot be empty");
    }

    [Fact]
    public void Constructor_WithNullEmail_ShouldThrowDomainException()
    {
        var action = () => new Email(null!);

        action.Should().Throw<DomainException>()
            .WithMessage("Email cannot be empty");
    }

    [Fact]
    public void Constructor_WithInvalidEmailFormat_ShouldThrowDomainException()
    {
        var action = () => new Email("invalid-email");

        action.Should().Throw<DomainException>()
            .WithMessage("Invalid email format");
    }

    [Fact]
    public void ImplicitOperator_ShouldConvertToString()
    {
        var emailString = "test@example.com";
        var email = new Email(emailString);

        string result = email;

        result.Should().Be(emailString.ToLowerInvariant().Trim());
    }

    [Fact]
    public void ToString_ShouldReturnValue()
    {
        var emailString = "test@example.com";
        var email = new Email(emailString);

        email.ToString().Should().Be(emailString.ToLowerInvariant().Trim());
    }
}

