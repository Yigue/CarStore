using Application.Abstractions.Messaging;
using Domain.Appointments;

namespace Application.Appointments.Commands.ChangeAppointmentStatus;

public sealed record ChangeAppointmentStatusCommand(
    Guid AppointmentId,
    AppointmentStatus TargetStatus) : ICommand;
