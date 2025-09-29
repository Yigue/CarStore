using Application.Quotes.Update;
using Domain.Quotes.Attributes;

namespace ApplicationTests.Quotes;

public class UpdateQuoteCommandValidatorTests
{
    private readonly UpdateQuoteCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_ForInvalidValues()
    {
        var command = new UpdateQuoteCommand(Guid.Empty, 0m, DateTime.UtcNow.AddDays(-1), QuoteStatus.Pending, string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateQuoteCommand.Id) && e.ErrorMessage == "'Id' must not be empty.");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateQuoteCommand.ValidUntil) && e.ErrorMessage.StartsWith("'Valid Until' must be greater than"));
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        var command = new UpdateQuoteCommand(Guid.NewGuid(), 1200m, DateTime.UtcNow.AddDays(1), QuoteStatus.Pending, "Ok");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
