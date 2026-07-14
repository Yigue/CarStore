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
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSaleCommand.Id));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSaleCommand.FinalPrice));
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        var command = new UpdateSaleCommand(Guid.NewGuid(), 1500m, PaymentMethod.Cash, SaleStatus.Pending, "CN1", "OK");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldPass_WhenContractNumberAndCommentsAreNull()
    {
        // Bug 3: ContractNumber/Comments are optional on update — the frontend may omit them.
        var command = new UpdateSaleCommand(Guid.NewGuid(), 1500m, PaymentMethod.Cash, SaleStatus.Pending);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenContractNumberProvided_And_ExceedsMaxLength()
    {
        var command = new UpdateSaleCommand(Guid.NewGuid(), 1500m, PaymentMethod.Cash, SaleStatus.Pending, new string('A', 51));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSaleCommand.ContractNumber));
    }

    [Fact]
    public void Validate_ShouldFail_WhenCommentsProvided_And_ExceedsMaxLength()
    {
        var command = new UpdateSaleCommand(Guid.NewGuid(), 1500m, PaymentMethod.Cash, SaleStatus.Pending, Comments: new string('B', 501));

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateSaleCommand.Comments));
    }
}
