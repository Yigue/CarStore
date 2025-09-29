using Application.Sales.Update;
using Domain.Financial.Attributes;
using Domain.Sales.Attributes;

namespace ApplicationTests.Sales;

public class UpdateSaleCommandValidatorTests
{
    private readonly UpdateSaleCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_ForInvalidValues()
    {
        var command = new UpdateSaleCommand(Guid.Empty, 0m, (PaymentMethod)999, (SaleStatus)999, string.Empty, string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSaleCommand.Id) && e.ErrorMessage == "'Id' must not be empty.");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSaleCommand.FinalPrice) && e.ErrorMessage == "'Final Price' must be greater than '0'.");
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        var command = new UpdateSaleCommand(Guid.NewGuid(), 1500m, PaymentMethod.Cash, SaleStatus.Pending, "CN1", "OK");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
