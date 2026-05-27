using Application.Abstractions.Messaging;

namespace Application.Appointments.Commands.DeleteAppointment;

public sealed record DeleteAppointmentCommand(Guid AppointmentId) : ICommand;
