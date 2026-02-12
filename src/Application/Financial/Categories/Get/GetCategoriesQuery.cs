using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;
using SharedKernel;

namespace Application.Financial.Categories.Get;

public sealed record GetCategoriesQuery : IQuery<List<TransactionCategory>>;
