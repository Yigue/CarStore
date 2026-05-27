using Application.Abstractions.Messaging;
using Domain.Appointments;

namespace Application.Appointments.Commands.CreateAppointment;

/// <summary>
/// Creates an appointment. <c>DealerId</c> is resolved server-side via
/// <see cref="Application.Abstractions.Tenancy.ICurrentTenantService"/> — never trust client input
/// for tenant scoping (multi-tenancy convention, same as <c>CreateLeadCommand</c>).
/// </summary>
public sealed record CreateAppointmentCommand(
    Guid VehicleId,
    Guid ClientId,
    Guid AgentId,
    DateTime Start,
    DateTime End,
    AppointmentType Type,
    string? Notes) : ICommand<Guid>;
