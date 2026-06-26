using Application.Clients.Update;
using Domain.Clients.Attributes;

namespace ApplicationTests.Clients;

public class UpdateClientCommandValidatorTests
{
    private readonly UpdateClientCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_ForInvalidValues()
    {
        var command = new UpdateClientCommand(Guid.Empty, "First", "Last", "123", "invalid-email", "123", "addr", ClientStatus.Active, ClientType.Individual);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateClientCommand.Id));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateClientCommand.Email));
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        var command = new UpdateClientCommand(Guid.NewGuid(), "First", "Last", "123", "user@mail.com", "123", "addr", ClientStatus.Active, ClientType.Individual);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_When_Type_Is_OutOfRange()
    {
        var command = new UpdateClientCommand(Guid.NewGuid(), "First", "Last", "123", "user@mail.com", "123", "addr", ClientStatus.Active, (ClientType)999);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateClientCommand.Type));
    }
}
