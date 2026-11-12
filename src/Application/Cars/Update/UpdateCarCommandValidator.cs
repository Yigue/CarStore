using FluentValidation;

namespace Application.Cars.Update;

internal sealed class UpdateCarCommandValidator : AbstractValidator<UpdateCarCommand>
{
    public UpdateCarCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty().WithMessage("El campo id es requerido");

        RuleFor(x => x.Marca).NotEmpty().WithMessage("El campo marca es requerido y debe ser una opcion valida");
        RuleFor(x => x.Modelo).NotEmpty().WithMessage("El campo modelo es requerido y debe ser una opcion valida");
        RuleFor(x => x.Color).IsInEnum().WithMessage("El campo color es requerido y debe ser una opcion valida");
        RuleFor(x => x.CarType).IsInEnum().WithMessage("El campo tipo de Car Type es requerido y debe ser una opcion valida");
        RuleFor(x => x.CarStatus).IsInEnum().WithMessage("El campo estado de Car Status es requerido y debe ser una opcion valida");
        RuleFor(x => x.ServiceCar).IsInEnum().WithMessage("El campo tipo de servicio es requerido y debe ser una opcion valida");
        RuleFor(x => x.CantidadPuertas).GreaterThanOrEqualTo(1).WithMessage("El campo cantidad de puertas debe ser mayor o igual a 1");
        RuleFor(x => x.CantidadAsientos).GreaterThanOrEqualTo(1).WithMessage("El campo cantidad de asientos debe ser mayor o igual a 1");
        RuleFor(x => x.Cilindrada).GreaterThanOrEqualTo(1).WithMessage("El campo cilindrada debe ser mayor o igual a 1");
        RuleFor(x => x.Kilometraje).GreaterThanOrEqualTo(0).WithMessage("El campo kilometraje debe ser mayor o igual a 0");

        RuleFor(x => x.Patente).NotEmpty().WithMessage("El campo patente es requerido");
        RuleFor(x => x.Patente).MaximumLength(10).WithMessage("El campo patente debe tener un maximo de 10 caracteres");

        RuleFor(x => x.Anio).NotEmpty().WithMessage("El campo anio es requerido");
        RuleFor(x => x.Anio).GreaterThan(0).WithMessage("El anio debe ser mayor a 0");
        RuleFor(x => x.Anio).LessThanOrEqualTo(DateTime.Now.Year).WithMessage("El anio debe ser valido y no puede ser mayor al anio actual");

        RuleFor(x => x.Descripcion).NotEmpty().WithMessage("El campo descripcion es requerido");
        RuleFor(x => x.Descripcion).MaximumLength(255).WithMessage("El campo descripcion debe tener un maximo de 255 caracteres");

        RuleFor(x => x.Price).NotEmpty().WithMessage("El campo precio es requerido");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("El campo precio debe ser mayor a 0");
    }
}
