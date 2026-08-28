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

    private static CreateCarCommand ValidCommand(
        string patente = "ABC123",
        string descripcion = "Test",
        decimal price = 10000m) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Color.Black, TypeCar.Sedan, StatusCar.New,
            StatusServiceCar.Service, 4, 5, 2000, 10000, 2020, patente, descripcion, price);

    /// <summary>
    /// A chained <c>.WithMessage()</c> applies to the last validator in the chain only. These four
    /// fields were written as <c>.NotEmpty().MaximumLength(n).WithMessage(...)</c>, so the Spanish
    /// message covered the length rule while the far more common empty-field case fell through to
    /// FluentValidation's English default — a generic error where the source clearly intended a
    /// specific one, with nothing in the file to hint at it.
    ///
    /// These assertions pin the message per rule, so re-collapsing the chains fails loudly.
    /// </summary>
    [Theory]
    [InlineData(nameof(CreateCarCommand.Descripcion), "El campo descripcion es requerido")]
    [InlineData(nameof(CreateCarCommand.Patente), "El campo patente es requerido")]
    public void Validate_ShouldReturnTheSpecificMessage_WhenARequiredTextFieldIsEmpty(
        string property, string expectedMessage)
    {
        CreateCarCommand command = property == nameof(CreateCarCommand.Descripcion)
            ? ValidCommand(descripcion: string.Empty)
            : ValidCommand(patente: string.Empty);

        var result = _validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == property && e.ErrorMessage == expectedMessage);
        result.Errors.Should().NotContain(e =>
            e.PropertyName == property && e.ErrorMessage.Contains("must not be empty"),
            "the caller must never see FluentValidation's English default");
    }

    [Fact]
    public void Validate_ShouldReturnTheSpecificMessage_WhenPriceIsZero()
    {
        var result = _validator.Validate(ValidCommand(price: 0m));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateCarCommand.Price) &&
            e.ErrorMessage == "El campo precio es requerido");
    }

    [Fact]
    public void Validate_ShouldStillEnforceTheLengthLimit_WithItsOwnMessage()
    {
        var result = _validator.Validate(ValidCommand(descripcion: new string('a', 256)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateCarCommand.Descripcion) &&
            e.ErrorMessage == "El campo descripcion debe tener un maximo de 255 caracteres");
    }
}
