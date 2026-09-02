using Application.Abstractions.Messaging;
using Domain.Leads;

namespace Application.Leads.GetAll;

public sealed record GetLeadsQuery(LeadStatus? Status, bool IncludeArchived = false) : IQuery<List<LeadResponse>>;
