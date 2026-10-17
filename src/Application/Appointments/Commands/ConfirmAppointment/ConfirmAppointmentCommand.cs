using Application.Abstractions.Messaging;

namespace Application.Appointments.Commands.ConfirmAppointment;

/// <summary>sfc B5.2.3: assigns an agent to a Requested appointment and schedules it.</summary>
public sealed record ConfirmAppointmentCommand(Guid AppointmentId, Guid AgentId) : ICommand;
