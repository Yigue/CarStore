using FluentValidation;

namespace Application.Appointments.Commands.RescheduleAppointment;

public sealed class RescheduleAppointmentCommandValidator : AbstractValidator<RescheduleAppointmentCommand>
{
    public RescheduleAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.End)
            .GreaterThan(x => x.Start)
            .WithMessage("La hora de fin debe ser posterior a la hora de inicio.");
    }
}
