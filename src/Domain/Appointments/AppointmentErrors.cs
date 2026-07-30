using SharedKernel;

namespace Domain.Appointments;

public static class AppointmentErrors
{
    public static Error NotFound(Guid appointmentId) => Error.NotFound(
        "Appointments.NotFound",
        $"The appointment with the Id = '{appointmentId}' was not found.");

    public static readonly Error Conflict = Error.Problem(
        "Appointments.Conflict",
        "El vehículo o el agente ya tiene una cita en ese horario.");

    public static readonly Error InvalidTimeRange = Error.Problem(
        "Appointments.InvalidTimeRange",
        "La hora de fin debe ser posterior a la hora de inicio.");

    public static Error InvalidStatusTransition(AppointmentStatus from, AppointmentStatus to) => Error.Problem(
        "Appointments.InvalidStatusTransition",
        $"No se puede cambiar el estado del turno de {from} a {to}.");
}
