using Application.Quotes.Create;

namespace ApplicationTests.Quotes;

public class CreateQuoteCommandValidatorTests
{
    private readonly CreateQuoteCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_ForInvalidValues()
    {
        var command = new CreateQuoteCommand(Guid.Empty, Guid.Empty, 0m, DateTime.UtcNow.AddDays(-1), string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateQuoteCommand.CarId) && e.ErrorMessage == "'Car Id' must not be empty.");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateQuoteCommand.ValidUntil) && e.ErrorMessage.StartsWith("'Valid Until' must be greater than"));
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        var command = new CreateQuoteCommand(Guid.NewGuid(), Guid.NewGuid(), 1000m, DateTime.UtcNow.AddDays(1), "Ok");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
