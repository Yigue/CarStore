using Domain.Appointments;
using FluentValidation;

namespace Application.Appointments.Commands.ChangeAppointmentStatus;

public sealed class ChangeAppointmentStatusCommandValidator : AbstractValidator<ChangeAppointmentStatusCommand>
{
    public ChangeAppointmentStatusCommandValidator()
    {
        RuleFor(x => x.AppointmentId)
            .NotEmpty().WithMessage("El ID del turno es requerido");

        RuleFor(x => x.TargetStatus)
            .IsInEnum().WithMessage("El estado de destino debe ser un valor válido")
            .NotEqual(AppointmentStatus.Scheduled).WithMessage("No se puede cambiar el estado a Programado");
    }
}
