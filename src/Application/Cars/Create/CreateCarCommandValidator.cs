using FluentValidation;

namespace Application.Cars.Create;

internal sealed class CreateCarCommandValidator : AbstractValidator<CreateCarCommand>
{
    public CreateCarCommandValidator()
    {
        RuleFor(x => x.Marca).NotEmpty().IsInEnum().WithMessage("El campo marca es requerido y debe ser una opcion valida");
        RuleFor(x => x.Modelo).NotEmpty().IsInEnum().WithMessage("El campo modelo es requerido y debe ser una opcion valida");
        RuleFor(x => x.Color).NotEmpty().IsInEnum().WithMessage("El campo color es requerido y debe ser una opcion valida");
        RuleFor(x => x.CarType).NotEmpty().IsInEnum().WithMessage("El campo tipo de vehiculo es requerido y debe ser una opcion valida");
        RuleFor(x => x.CarStatus).NotEmpty().IsInEnum().WithMessage("El campo estado de vehiculo es requerido y debe ser una opcion valida");
        RuleFor(x => x.ServiceCar).NotEmpty().IsInEnum().WithMessage("El campo tipo de servicio es requerido y debe ser una opcion valida");
        RuleFor(x => x.CantidadPuertas).GreaterThanOrEqualTo(1).WithMessage("El campo cantidad de puertas debe ser mayor o igual a 1");
        RuleFor(x => x.CantidadAsientos).GreaterThanOrEqualTo(1).WithMessage("El campo cantidad de asientos debe ser mayor o igual a 1");
        RuleFor(x => x.Cilindrada).GreaterThanOrEqualTo(1).WithMessage("El campo cilindrada debe ser mayor o igual a 1");
        RuleFor(x => x.Kilometraje).GreaterThanOrEqualTo(1).WithMessage("El campo kilometraje debe ser mayor o igual a 1");
        RuleFor(x => x.Patente).NotEmpty().MaximumLength(10).WithMessage("El campo patente es requerido y debe tener un maximo de 10 caracteres");
        RuleFor(x => x.Anio).NotEmpty().GreaterThan(0).LessThanOrEqualTo(DateTime.Now.Year).WithMessage("El año debe ser válido y no puede ser mayor al año actual");
        RuleFor(x => x.Descripcion).NotEmpty().MaximumLength(255).WithMessage("El campo descripcion es requerido y debe tener un maximo de 255 caracteres");
        RuleFor(x => x.Price).NotEmpty().GreaterThan(0).WithMessage("El campo precio es requerido y debe ser mayor a 0");
    }
}

