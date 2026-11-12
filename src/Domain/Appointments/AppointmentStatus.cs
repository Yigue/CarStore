namespace Domain.Appointments;

public enum AppointmentStatus
{
    Scheduled = 0,
    Completed = 1,
    Cancelled = 2,
    NoShow = 3,
    // sfc B5.2.3: appended (persisted as string, so no data migration). Awaiting agent confirmation.
    Requested = 4
}
