using Application.Quotes.Create;
using Domain.Quotes.Attributes;

namespace ApplicationTests.Quotes;

public class CreateQuoteCommandValidatorTests
{
    private readonly CreateQuoteCommandValidator _validator = new();

    private static CreateQuoteCommand ValidBaseCommand(Guid? clientId, Guid? leadId) =>
        new CreateQuoteCommand(
            Guid.NewGuid(),
            clientId,
            leadId,
            1000m,
            PaymentMethod.Contado,
            DateTime.UtcNow.AddDays(1),
            "Ok");

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

    // Phase 5: XOR invariant contract lock (expected GREEN)

    [Fact]
    public void Validate_ShouldFail_WhenBothClientIdAndLeadIdAreNull()
    {
        var command = ValidBaseCommand(clientId: null, leadId: null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse("a quote must reference exactly one party");
    }

    [Fact]
    public void Validate_ShouldFail_WhenBothClientIdAndLeadIdAreProvided()
    {
        var command = ValidBaseCommand(clientId: Guid.NewGuid(), leadId: Guid.NewGuid());

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse("a quote cannot reference both a client and a lead");
    }

    [Fact]
    public void Validate_ShouldPass_WhenOnlyClientIdIsProvided()
    {
        var command = ValidBaseCommand(clientId: Guid.NewGuid(), leadId: null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue("a client-only quote is valid");
    }

    [Fact]
    public void Validate_ShouldPass_WhenOnlyLeadIdIsProvided()
    {
        var command = ValidBaseCommand(clientId: null, leadId: Guid.NewGuid());

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue("a lead-only quote is valid");
    }
}
