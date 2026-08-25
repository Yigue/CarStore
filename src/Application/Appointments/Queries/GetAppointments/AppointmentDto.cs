using Domain.Appointments;

namespace Application.Appointments.Queries.GetAppointments;

public sealed record AppointmentDto(
    Guid Id,
    Guid VehicleId,
    string? VehicleName,
    Guid? ClientId,
    // Un turno referencia exactamente uno de ClientId o LeadId — lo exige
    // CreateAppointmentCommandValidator (`ClientId.HasValue ^ LeadId.HasValue`).
    // LeadId no se proyectaba, así que la mitad de los turnos válidos llegaban
    // al cliente sin forma de volver a su origen: el nombre se veía, porque
    // ClientName cae al del lead, pero el id no viajaba y no había a dónde
    // navegar. El frontend ya declaraba `leadId` en su propio AppointmentDto,
    // de modo que el campo estaba tipado y siempre indefinido.
    Guid? LeadId,
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
