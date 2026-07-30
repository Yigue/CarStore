using Domain.Appointments;

namespace Application.Appointments.Queries.GetAppointments;

public sealed record AppointmentDto(
    Guid Id,
    Guid VehicleId,
    string? VehicleName,
    Guid? ClientId,
    string? ClientName,
    Guid AgentId,
    string? AgentName,
    DateTime Start,
    DateTime End,
    AppointmentType Type,
    string TypeDisplay,
    AppointmentStatus Status,
    string StatusDisplay,
    string? Notes,
    DateTime CreatedAt);
