using System;
using Application.Leads.Convert;
using Domain.Clients.Attributes;

namespace Application.UnitTests.Leads;

public class ConvertLeadToClientCommandValidatorTests
{
    private readonly ConvertLeadToClientCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenDniIsEmpty()
    {
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), string.Empty, "Av Real 123", ClientType.Individual);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ConvertLeadToClientCommand.Dni));
    }

    [Fact]
    public void Validate_ShouldFail_WhenDniIsWhitespace()
    {
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), "   ", "Av Real 123", ClientType.Individual);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldPass_WhenDniIsProvided()
    {
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), "28345678", "Av Real 123", ClientType.Individual);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    // Phase 3 RED: Address validation

    [Fact]
    public void Validate_ShouldFail_WhenAddressIsEmpty()
    {
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), "28345678", string.Empty, ClientType.Individual);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ConvertLeadToClientCommand.Address));
    }

    [Fact]
    public void Validate_ShouldFail_WhenAddressIsWhitespace()
    {
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), "28345678", "   ", ClientType.Individual);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ConvertLeadToClientCommand.Address));
    }

    [Fact]
    public void Validate_ShouldPass_WhenDniAndAddressAreProvided()
    {
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), "28345678", "Av. Corrientes 1234", ClientType.Individual);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    // PR1: Type must be required

    [Fact]
    public void Validate_ShouldFail_WhenTypeIsOutOfRange()
    {
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), "28345678", "Av. Corrientes 1234", (ClientType)999);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ConvertLeadToClientCommand.Type));
    }
}
