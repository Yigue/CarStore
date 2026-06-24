using Application.Abstractions.Messaging;

namespace Application.Leads.Archive;

public sealed record ArchiveLeadCommand(Guid LeadId) : ICommand;
