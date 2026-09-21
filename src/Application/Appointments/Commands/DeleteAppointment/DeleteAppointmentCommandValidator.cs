using FluentValidation;

namespace Application.Appointments.Commands.DeleteAppointment;

public sealed class DeleteAppointmentCommandValidator : AbstractValidator<DeleteAppointmentCommand>
{
    public DeleteAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
    }
}
