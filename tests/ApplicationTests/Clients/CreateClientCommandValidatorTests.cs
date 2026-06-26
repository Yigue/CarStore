using Application.Clients.Create;
using Domain.Clients.Attributes;

namespace ApplicationTests.Clients;

public class CreateClientCommandValidatorTests
{
    private readonly CreateClientCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_ForInvalidValues()
    {
        var command = new CreateClientCommand(string.Empty, "Last", new string('1', 21), "e@mail.com", "123", "addr", ClientType.Individual);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateClientCommand.FirstName));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateClientCommand.DNI));
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        var command = new CreateClientCommand("Name", "Last", "123456", "e@mail.com", "123", "addr", ClientType.Individual);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_When_Type_Is_OutOfRange()
    {
        var command = new CreateClientCommand("Name", "Last", "123456", "e@mail.com", "123", "addr", (ClientType)999);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateClientCommand.Type));
    }
}
