using FluentValidation;

namespace Application.Appointments.Commands.ConfirmAppointment;

public sealed class ConfirmAppointmentCommandValidator : AbstractValidator<ConfirmAppointmentCommand>
{
    public ConfirmAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.AgentId).NotEmpty();
    }
}
