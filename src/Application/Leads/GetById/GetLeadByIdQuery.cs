using Application.Abstractions.Messaging;

namespace Application.Leads.GetById;

public sealed record GetLeadByIdQuery(Guid LeadId) : IQuery<LeadDetailResponse>;
