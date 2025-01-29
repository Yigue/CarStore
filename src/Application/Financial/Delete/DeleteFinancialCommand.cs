using Application.Abstractions.Messaging;

namespace Application.Financial.Delete;

public sealed record DeleteFinancialCommand(Guid Id) : ICommand<Guid>;
