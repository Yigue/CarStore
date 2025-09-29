using Application.Cars.Update;
using Domain.Cars.Atribbutes;

namespace ApplicationTests.Cars;

public class UpdateCarCommandValidatorTests
{
    private readonly UpdateCarCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenIdEmpty()
    {
        var command = new UpdateCarCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Color.Black, TypeCar.Sedan, StatusCar.New, statusServiceCar.Service, 4, 4, 1000, 1000, DateTime.Now.Year, "ABC123", "Desc", 1000m);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateCarCommand.Id) && e.ErrorMessage == "'Id' must not be empty.");
    }

    [Fact]
    public void Validate_ShouldPass_WhenIdProvided()
    {
        var command = new UpdateCarCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Color.Black, TypeCar.Sedan, StatusCar.New, statusServiceCar.Service, 4, 4, 1000, 1000, DateTime.Now.Year, "ABC123", "Desc", 1000m);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
