using Application.Sales.Create;
using Domain.Financial.Attributes;
using Domain.Sales.Attributes;

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
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSaleCommand.CarId));
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSaleCommand.FinalPrice));
    }

    [Fact]
    public void Validate_ShouldPass_WhenDataValid()
    {
        var command = new CreateSaleCommand(Guid.NewGuid(), Guid.NewGuid(), 1000m, PaymentMethod.Cash, "CN1", "All good");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldPass_WhenStatusIsNotProvided()
    {
        var command = new CreateSaleCommand(Guid.NewGuid(), Guid.NewGuid(), 1000m, PaymentMethod.Cash, "CN1", "All good");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldPass_WhenStatusIsPendingOrCompleted()
    {
        var pending = new CreateSaleCommand(Guid.NewGuid(), Guid.NewGuid(), 1000m, PaymentMethod.Cash, "CN1", "ok", Status: SaleStatus.Pending);
        var completed = new CreateSaleCommand(Guid.NewGuid(), Guid.NewGuid(), 1000m, PaymentMethod.Cash, "CN2", "ok", Status: SaleStatus.Completed);

        _validator.Validate(pending).IsValid.Should().BeTrue();
        _validator.Validate(completed).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenStatusIsCancelled()
    {
        // Bug 1: a sale cannot be created as already Cancelled.
        var command = new CreateSaleCommand(Guid.NewGuid(), Guid.NewGuid(), 1000m, PaymentMethod.Cash, "CN1", "ok", Status: SaleStatus.Cancelled);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSaleCommand.Status));
    }

    // ContractNumber is NOT NULL in the database (SaleConfiguration.cs) but had no validator
    // rule, so an omitted value reached Postgres and came back as a raw 500 instead of a 400 a
    // caller could act on.

    [Fact]
    public void Validate_ShouldFail_WhenContractNumberIsEmpty()
    {
        var command = new CreateSaleCommand(Guid.NewGuid(), Guid.NewGuid(), 1000m, PaymentMethod.Cash, string.Empty, "comments");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSaleCommand.ContractNumber));
    }

    [Fact]
    public void Validate_ShouldFail_WhenContractNumberExceedsFiftyCharacters()
    {
        var command = new CreateSaleCommand(Guid.NewGuid(), Guid.NewGuid(), 1000m, PaymentMethod.Cash, new string('C', 51), "comments");

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateSaleCommand.ContractNumber));
    }
}
