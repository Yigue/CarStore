using Application.Abstractions.Messaging;

namespace Application.Leads.GetActivity;

public sealed record GetLeadActivityQuery(
    Guid LeadId,
    int Page = 1,
    int PageSize = 50) : IQuery<LeadActivityResponse>;
