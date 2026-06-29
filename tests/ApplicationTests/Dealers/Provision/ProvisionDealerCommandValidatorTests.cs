using Application.Dealers.Provision;

namespace ApplicationTests.Dealers.Provision;

public class ProvisionDealerCommandValidatorTests
{
    private readonly ProvisionDealerCommandValidator _validator = new();

    private static ProvisionDealerCommand Valid() => new(
        DealerName: "Automotors del Sur",
        Subdomain: "automotors",
        AdminEmail: "admin@automotors.com",
        AdminPassword: "Sup3r$ecret!",
        AdminFirstName: "Ana",
        AdminLastName: "García");

    [Fact]
    public void Validate_ShouldPass_ForValidCommand()
    {
        var result = _validator.Validate(Valid());

        result.IsValid.Should().BeTrue(string.Join(", ", result.Errors));
    }

    [Fact]
    public void Validate_ShouldFail_WhenDealerNameTooShort()
    {
        var cmd = Valid() with { DealerName = "A" };
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ProvisionDealerCommand.DealerName));
    }

    [Fact]
    public void Validate_ShouldFail_WhenDealerNameTooLong()
    {
        var cmd = Valid() with { DealerName = new string('x', 201) };
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ProvisionDealerCommand.DealerName));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("api")]
    [InlineData("www")]
    [InlineData("app")]
    [InlineData("mail")]
    [InlineData("support")]
    [InlineData("dashboard")]
    [InlineData("static")]
    [InlineData("cdn")]
    [InlineData("auth")]
    [InlineData("help")]
    [InlineData("status")]
    [InlineData("billing")]
    [InlineData("root")]
    [InlineData("system")]
    [InlineData("internal")]
    public void Validate_ShouldFail_WhenSubdomainIsReserved(string reserved)
    {
        var cmd = Valid() with { Subdomain = reserved };
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ProvisionDealerCommand.Subdomain));
    }

    [Theory]
    [InlineData("Auto Motors!")]      // spaces + uppercase + symbol
    [InlineData("AutoMotors")]        // uppercase letters
    [InlineData("-leading")]          // leading hyphen
    [InlineData("trailing-")]         // trailing hyphen
    [InlineData("ab")]                // too short
    [InlineData("a")]                 // way too short
    public void Validate_ShouldFail_WhenSubdomainShapeInvalid(string slug)
    {
        var cmd = Valid() with { Subdomain = slug };
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ProvisionDealerCommand.Subdomain));
    }

    [Fact]
    public void Validate_ShouldFail_WhenSubdomainExceeds32Chars()
    {
        var cmd = Valid() with { Subdomain = new string('a', 33) };
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ProvisionDealerCommand.Subdomain));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("plaintext")]
    [InlineData("@no-local.com")]
    [InlineData("")]
    public void Validate_ShouldFail_WhenAdminEmailInvalid(string email)
    {
        var cmd = Valid() with { AdminEmail = email };
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ProvisionDealerCommand.AdminEmail));
    }

    [Theory]
    [InlineData("password")]      // missing upper + digit + symbol + too short
    [InlineData("Password1")]     // no symbol
    [InlineData("password1!")]    // no upper
    [InlineData("PASSWORD1!")]    // no lower
    [InlineData("Password!")]     // no digit
    [InlineData("Pass1!")]        // too short (6 chars)
    [InlineData("NineChars1")]    // 10 chars but no symbol
    public void Validate_ShouldFail_WhenPasswordIsWeak(string password)
    {
        var cmd = Valid() with { AdminPassword = password };
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ProvisionDealerCommand.AdminPassword));
    }

    [Fact]
    public void Validate_ShouldFail_WhenFirstNameEmpty()
    {
        var cmd = Valid() with { AdminFirstName = "" };
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ProvisionDealerCommand.AdminFirstName));
    }

    [Fact]
    public void Validate_ShouldFail_WhenLastNameEmpty()
    {
        var cmd = Valid() with { AdminLastName = "" };
        var result = _validator.Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ProvisionDealerCommand.AdminLastName));
    }
}