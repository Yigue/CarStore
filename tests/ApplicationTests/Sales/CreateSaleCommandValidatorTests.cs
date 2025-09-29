using Application.Sales.Create;
using Domain.Financial.Attributes;

namespace ApplicationTests.Sales;

public class CreateSaleCommandValidatorTests
{
    private readonly CreateSaleCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenRequiredFieldsInvalid()
    {
        var command = new CreateSaleCommand(Guid.Empty, Guid.Empty, 0m, (PaymentMethod)999, string.Empty, string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSaleCommand.CarId) && e.ErrorMessage == "'Car Id' must not be empty.");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSaleCommand.FinalPrice) && e.ErrorMessage == "'Final Price' must be greater than '0'.");
    }

    [Fact]
    public void Validate_ShouldPass_WhenDataValid()
    {
        var command = new CreateSaleCommand(Guid.NewGuid(), Guid.NewGuid(), 1000m, PaymentMethod.Cash, "CN1", "All good");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
