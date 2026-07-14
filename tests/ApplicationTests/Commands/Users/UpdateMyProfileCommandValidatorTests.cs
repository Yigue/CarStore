using Application.Users.Commands.UpdateMyProfile;
using Xunit;

namespace Application.UnitTests.Commands.Users;

public class UpdateMyProfileCommandValidatorTests
{
    private readonly UpdateMyProfileValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenFirstNameIsEmpty()
    {
        var command = new UpdateMyProfileCommand("", "Last", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateMyProfileCommand.FirstName));
    }

    [Fact]
    public void Validate_ShouldFail_WhenLastNameIsEmpty()
    {
        var command = new UpdateMyProfileCommand("First", "", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateMyProfileCommand.LastName));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPhoneExceedsMaxLength()
    {
        var command = new UpdateMyProfileCommand("First", "Last", new string('1', 21));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateMyProfileCommand.Phone));
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        var command = new UpdateMyProfileCommand("First", "Last", "+5491112345678");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldPass_WhenPhoneIsNull()
    {
        var command = new UpdateMyProfileCommand("First", "Last", null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
