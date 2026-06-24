using Application.Abstractions.Messaging;

namespace Application.Leads.LinkVehicle;

public sealed record LinkVehicleToLeadCommand(Guid LeadId, Guid VehicleId) : ICommand;
