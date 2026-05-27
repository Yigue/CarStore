using Application.Abstractions.Messaging;

namespace Application.Appointments.Queries.GetAppointments;

public sealed record GetAppointmentsQuery(DateTime From, DateTime To) : IQuery<IReadOnlyList<AppointmentDto>>;
