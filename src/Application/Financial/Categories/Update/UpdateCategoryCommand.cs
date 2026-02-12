using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;

namespace Application.Financial.Categories.Update;

public sealed record UpdateCategoryCommand(
    Guid Id,
    string Name,
    string Description,
    TransactionType Type) : ICommand;
