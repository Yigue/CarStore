using Application.Abstractions.Messaging;

namespace Application.Appointments.Commands.RescheduleAppointment;

public sealed record RescheduleAppointmentCommand(
    Guid AppointmentId,
    DateTime Start,
    DateTime End) : ICommand;
