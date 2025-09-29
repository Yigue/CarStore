using Application.Clients.Create;

namespace ApplicationTests.Clients;

public class CreateClientCommandValidatorTests
{
    private readonly CreateClientCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_ForInvalidValues()
    {
        var command = new CreateClientCommand(string.Empty, "Last", new string('1', 21), "e@mail.com", "123", "addr");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateClientCommand.FirstName) && e.ErrorMessage == "'First Name' must not be empty.");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateClientCommand.DNI) && e.ErrorMessage.StartsWith("'D N I' must be 20 characters or fewer"));
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        var command = new CreateClientCommand("Name", "Last", "123456", "e@mail.com", "123", "addr");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
