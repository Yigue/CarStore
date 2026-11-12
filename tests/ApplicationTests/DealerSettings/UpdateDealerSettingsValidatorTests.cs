using Application.DealerSettings.Update;

namespace ApplicationTests.DealerSettings;

public class UpdateDealerSettingsValidatorTests
{
    private readonly UpdateDealerSettingsCommandValidator _validator = new();

    private static UpdateDealerSettingsCommand Valid(
        string dealerName = "Autos Sur",
        string contactEmail = "ventas@autossur.com",
        string? facebookUrl = "https://facebook.com/autossur",
        string? instagramUrl = null,
        string? twitterUrl = null,
        string? phoneNumber = "+54 11 4444-5555",
        decimal? interestRateTna = 45m) =>
        new(dealerName, contactEmail, true, null, null, "Av. Siempre Viva 123", phoneNumber,
            facebookUrl, instagramUrl, twitterUrl, interestRateTna);

    [Fact]
    public void Validate_ShouldPass_ForValidPayload() =>
        _validator.Validate(Valid()).IsValid.Should().BeTrue();

    [Fact]
    public void Validate_ShouldPass_WhenOptionalFieldsAreNull() =>
        _validator.Validate(Valid(facebookUrl: null, phoneNumber: null, interestRateTna: null)).IsValid.Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_ForEmptyDealerName(string name) =>
        _validator.Validate(Valid(dealerName: name)).Errors
            .Should().Contain(e => e.PropertyName == nameof(UpdateDealerSettingsCommand.DealerName));

    [Fact]
    public void Validate_ShouldFail_ForDealerNameLongerThanColumn() =>
        _validator.Validate(Valid(dealerName: new string('a', 201))).Errors
            .Should().Contain(e => e.PropertyName == nameof(UpdateDealerSettingsCommand.DealerName));

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_ShouldFail_ForInvalidContactEmail(string email) =>
        _validator.Validate(Valid(contactEmail: email)).Errors
            .Should().Contain(e => e.PropertyName == nameof(UpdateDealerSettingsCommand.ContactEmail));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>1</script>")]
    [InlineData("/relative/path")]
    [InlineData("ftp://example.com/x")]
    public void Validate_ShouldFail_ForNonHttpSocialUrls(string url)
    {
        _validator.Validate(Valid(facebookUrl: url)).Errors
            .Should().Contain(e => e.PropertyName == nameof(UpdateDealerSettingsCommand.FacebookUrl));
        _validator.Validate(Valid(instagramUrl: url)).Errors
            .Should().Contain(e => e.PropertyName == nameof(UpdateDealerSettingsCommand.InstagramUrl));
        _validator.Validate(Valid(twitterUrl: url)).Errors
            .Should().Contain(e => e.PropertyName == nameof(UpdateDealerSettingsCommand.TwitterUrl));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000.5)]
    public void Validate_ShouldFail_ForOutOfRangeInterestRate(double rate) =>
        _validator.Validate(Valid(interestRateTna: (decimal)rate)).Errors
            .Should().Contain(e => e.PropertyName == nameof(UpdateDealerSettingsCommand.InterestRateTna));

    [Fact]
    public void Validate_ShouldFail_ForOverlongPhoneNumber() =>
        _validator.Validate(Valid(phoneNumber: new string('1', 51))).Errors
            .Should().Contain(e => e.PropertyName == nameof(UpdateDealerSettingsCommand.PhoneNumber));
}
