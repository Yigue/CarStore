using Application.Financial.Create;
using Domain.Financial.Attributes;

namespace ApplicationTests.Financial;

public class CreateFinancialCommandValidatorTests
{
    private readonly CreateFinancialCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_ForInvalidValues()
    {
        var command = new CreateFinancialCommand((TransactionType)999, 0m, string.Empty, (PaymentMethod)999, Guid.Empty, null, null, null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateFinancialCommand.Type) && e.ErrorMessage == "El tipo de transacción debe ser un valor válido");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateFinancialCommand.Amount) && e.ErrorMessage == "El monto debe ser mayor que cero");
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        var command = new CreateFinancialCommand(TransactionType.Income, 100m, "desc", PaymentMethod.Cash, Guid.NewGuid(), null, null, null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
