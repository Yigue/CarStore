using Application.Clients.Update;
using Domain.Clients.Attributes;

namespace ApplicationTests.Clients;

public class UpdateClientCommandValidatorTests
{
    private readonly UpdateClientCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_ForInvalidValues()
    {
        var command = new UpdateClientCommand(Guid.Empty, "First", "Last", "123", "invalid-email", "123", "addr", ClientStatus.Active);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateClientCommand.Id) && e.ErrorMessage == "'Id' must not be empty.");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateClientCommand.Email) && e.ErrorMessage == "'Email' is not a valid email address.");
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        var command = new UpdateClientCommand(Guid.NewGuid(), "First", "Last", "123", "user@mail.com", "123", "addr", ClientStatus.Active);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
