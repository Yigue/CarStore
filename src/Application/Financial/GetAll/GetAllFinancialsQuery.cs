using Application.Abstractions.Messaging;

namespace Application.Financial.GetAll;

public sealed record GetAllFinancialsQuery : IQuery<IReadOnlyList<FinancialResponses>>;
