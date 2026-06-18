using Application.Quotes.Create;
using Domain.Quotes.Attributes;

namespace ApplicationTests.Quotes;

public class CreateQuoteCommandValidatorTests
{
    private readonly CreateQuoteCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_ForInvalidValues()
    {
        var command = new CreateQuoteCommand(Guid.Empty, Guid.Empty, Guid.Empty, 0m, (PaymentMethod)999, DateTime.UtcNow.AddDays(-1), string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateQuoteCommand.CarId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateQuoteCommand.ValidUntil));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateQuoteCommand.PaymentMethod));
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        // Exactly one party (client XOR lead) is required.
        var command = new CreateQuoteCommand(Guid.NewGuid(), Guid.NewGuid(), null, 1000m, PaymentMethod.Contado, DateTime.UtcNow.AddDays(1), "Ok");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
