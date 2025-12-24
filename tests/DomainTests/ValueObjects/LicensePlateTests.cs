using Domain.Shared.ValueObjects;
using SharedKernel;

namespace DomainTests.ValueObjects;

public class LicensePlateTests
{
    [Fact]
    public void Constructor_WithValidLicensePlate_ShouldCreateLicensePlate()
    {
        var plateString = "ABC123";
        var licensePlate = new LicensePlate(plateString);

        licensePlate.Value.Should().Be(plateString.ToUpperInvariant());
    }

    [Fact]
    public void Constructor_WithLicensePlateWithSpaces_ShouldRemoveSpaces()
    {
        var plateString = "ABC 123";
        var licensePlate = new LicensePlate(plateString);

        licensePlate.Value.Should().Be("ABC123");
    }

    [Fact]
    public void Constructor_WithLicensePlateWithDashes_ShouldRemoveDashes()
    {
        var plateString = "ABC-123";
        var licensePlate = new LicensePlate(plateString);

        licensePlate.Value.Should().Be("ABC123");
    }

    [Fact]
    public void Constructor_WithLicensePlateWithLowerCase_ShouldConvertToUpper()
    {
        var plateString = "abc123";
        var licensePlate = new LicensePlate(plateString);

        licensePlate.Value.Should().Be("ABC123");
    }

    [Fact]
    public void Constructor_WithEmptyLicensePlate_ShouldThrowDomainException()
    {
        var action = () => new LicensePlate("");

        action.Should().Throw<DomainException>()
            .WithMessage("License plate cannot be empty");
    }

    [Fact]
    public void Constructor_WithNullLicensePlate_ShouldThrowDomainException()
    {
        var action = () => new LicensePlate(null!);

        action.Should().Throw<DomainException>()
            .WithMessage("License plate cannot be empty");
    }

    [Fact]
    public void Constructor_WithTooShortLicensePlate_ShouldThrowDomainException()
    {
        var action = () => new LicensePlate("AB12");

        action.Should().Throw<DomainException>()
            .WithMessage("License plate must be between 5 and 8 characters");
    }

    [Fact]
    public void Constructor_WithTooLongLicensePlate_ShouldThrowDomainException()
    {
        var action = () => new LicensePlate("ABCD12345");

        action.Should().Throw<DomainException>()
            .WithMessage("License plate must be between 5 and 8 characters");
    }

    [Fact]
    public void Constructor_WithInvalidFormat_ShouldThrowDomainException()
    {
        var action = () => new LicensePlate("123ABC");

        action.Should().Throw<DomainException>()
            .WithMessage("Invalid license plate format");
    }

    [Fact]
    public void ImplicitOperator_ShouldConvertToString()
    {
        var plateString = "ABC123";
        var licensePlate = new LicensePlate(plateString);

        string result = licensePlate;

        result.Should().Be(plateString.ToUpperInvariant());
    }

    [Fact]
    public void ToString_ShouldReturnValue()
    {
        var plateString = "ABC123";
        var licensePlate = new LicensePlate(plateString);

        licensePlate.ToString().Should().Be(plateString.ToUpperInvariant());
    }

    [Theory]
    [InlineData("ABC123")]
    [InlineData("AB123CD")]
    [InlineData("ABC1234")]
    public void Constructor_WithValidFormats_ShouldCreateLicensePlate(string plate)
    {
        var licensePlate = new LicensePlate(plate);

        licensePlate.Value.Should().NotBeNullOrEmpty();
    }
}

