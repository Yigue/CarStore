using Application.Cars.Create;
using Domain.Cars.Atribbutes;

namespace ApplicationTests.Cars;

public class CreateCarCommandValidatorTests
{
    private readonly CreateCarCommandValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_ForInvalidValues()
    {
        var command = new CreateCarCommand
        {
            Marca = Guid.Empty,
            Modelo = Guid.NewGuid(),
            Color = Color.Black,
            CarType = TypeCar.Sedan,
            CarStatus = StatusCar.New,
            ServiceCar = statusServiceCar.Service,
            CantidadPuertas = 0,
            CantidadAsientos = 0,
            Cilindrada = 0,
            Kilometraje = 0,
            Patente = string.Empty,
            Año = DateTime.Now.Year + 1,
            Descripcion = string.Empty,
            Price = 0m
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCarCommand.Marca) && e.ErrorMessage == "El campo marca es requerido y debe ser una opcion valida");
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateCarCommand.Año) && e.ErrorMessage == "El año debe ser válido y no puede ser mayor al año actual");
    }

    [Fact]
    public void Validate_ShouldPass_ForValidValues()
    {
        var command = new CreateCarCommand
        {
            Marca = Guid.NewGuid(),
            Modelo = Guid.NewGuid(),
            Color = Color.Black,
            CarType = TypeCar.Sedan,
            CarStatus = StatusCar.New,
            ServiceCar = statusServiceCar.Service,
            CantidadPuertas = 4,
            CantidadAsientos = 4,
            Cilindrada = 1000,
            Kilometraje = 1000,
            Patente = "ABC123",
            Año = DateTime.Now.Year,
            Descripcion = "Desc",
            Price = 1000m
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }
}
