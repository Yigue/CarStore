using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;

namespace Application.Financial.Categories.Create;

public sealed record CreateCategoryCommand(
    string Name,
    string Description,
    TransactionType Type) : ICommand<Guid>;
