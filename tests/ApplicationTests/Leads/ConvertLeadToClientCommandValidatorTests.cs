using System;
using Application.Leads.Convert;

namespace Application.UnitTests.Leads;

public class ConvertLeadToClientCommandValidatorTests
{
    private readonly ConvertLeadToClientCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenDniIsEmpty()
    {
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), string.Empty, "Av Real 123");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ConvertLeadToClientCommand.Dni));
    }

    [Fact]
    public void Validate_ShouldFail_WhenDniIsWhitespace()
    {
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), "   ", "Av Real 123");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldPass_WhenDniIsProvided()
    {
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), "28345678", "Av Real 123");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    // Phase 3 RED: Address validation

    [Fact]
    public void Validate_ShouldFail_WhenAddressIsEmpty()
    {
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), "28345678", string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ConvertLeadToClientCommand.Address));
    }

    [Fact]
    public void Validate_ShouldFail_WhenAddressIsWhitespace()
    {
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), "28345678", "   ");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ConvertLeadToClientCommand.Address));
    }

    [Fact]
    public void Validate_ShouldPass_WhenDniAndAddressAreProvided()
    {
        var command = new ConvertLeadToClientCommand(Guid.NewGuid(), "28345678", "Av. Corrientes 1234");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
