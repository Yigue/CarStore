using Application.Users.Register;

namespace ApplicationTests.Users;

public class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_ForInvalidValues()
    {
        var command = new RegisterUserCommand("user@mail.com", string.Empty, "Last", "1234");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterUserCommand.FirstName) && e.ErrorMessage == "'First Name' must not be empty.");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterUserCommand.Password) && e.ErrorMessage.StartsWith("'Password' must be at least 8 characters"));
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        var command = new RegisterUserCommand("user@mail.com", "First", "Last", "12345678");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
