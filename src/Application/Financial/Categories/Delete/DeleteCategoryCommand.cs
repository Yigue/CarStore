using Application.Abstractions.Messaging;

namespace Application.Financial.Categories.Delete;

public sealed record DeleteCategoryCommand(Guid Id) : ICommand;
