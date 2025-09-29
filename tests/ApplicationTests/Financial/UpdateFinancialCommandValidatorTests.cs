using Application.Financial.Update;
using Domain.Financial.Attributes;

namespace ApplicationTests.Financial;

public class UpdateFinancialCommandValidatorTests
{
    private readonly UpdateFinancialCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_ForInvalidValues()
    {
        var command = new UpdateFinancialCommand(Guid.Empty, (TransactionType)999, 0m, string.Empty, (PaymentMethod)999, null, DateTime.UtcNow.AddDays(1), Guid.Empty, null, null, null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateFinancialCommand.Id) && e.ErrorMessage == "El ID es requerido");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateFinancialCommand.TransactionDate) && e.ErrorMessage == "La fecha de transacción no puede ser futura");
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        var command = new UpdateFinancialCommand(Guid.NewGuid(), TransactionType.Income, 100m, "desc", PaymentMethod.Cash, null, DateTime.UtcNow, Guid.NewGuid(), null, null, null);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
