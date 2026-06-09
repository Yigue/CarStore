using Application.Abstractions.Messaging;

namespace Application.Leads.UpdateNotes;

public sealed record UpdateLeadNotesCommand(Guid LeadId, string? Notes) : ICommand;
