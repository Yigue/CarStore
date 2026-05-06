using Application.Cars.Create;
using Domain.Cars.Attributes;

namespace ApplicationTests.Cars;

public class CreateCarCommandValidatorTests
{
    private readonly CreateCarCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_ForInvalidValues()
    {
        var command = new CreateCarCommand(
            Guid.Empty,
            Guid.NewGuid(),
            Color.Black,
            TypeCar.Sedan,
            StatusCar.New,
            StatusServiceCar.Service,
            0,
            0,
            0,
            0,
            DateTime.Now.Year + 1,
            string.Empty,
            string.Empty,
            0m);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCarCommand.Marca) && e.ErrorMessage == "El campo marca es requerido y debe ser una opcion valida");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCarCommand.Anio) && e.ErrorMessage == "El anio debe ser valido y no puede ser mayor al anio actual");
        }
        [Fact]
        public void Validate_ShouldPass_ForValidValues()
        {
        var command = new CreateCarCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Color.Black,
            TypeCar.Sedan,
            StatusCar.New,
            StatusServiceCar.Service,
            4,
            5,
            2000,
            10000,
            2020,
            "ABC123",
            "Test",
            10000m);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
