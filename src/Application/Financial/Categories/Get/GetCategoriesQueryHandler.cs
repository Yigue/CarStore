using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;
using SharedKernel;

namespace Application.Financial.Categories.Get;

internal sealed class GetCategoriesQueryHandler(ICachedCategoryService categoryService)
    : IQueryHandler<GetCategoriesQuery, List<TransactionCategory>>
{
    public async Task<Result<List<TransactionCategory>>> Handle(GetCategoriesQuery query, CancellationToken cancellationToken)
    {
        var categories = await categoryService.GetAllAsync(cancellationToken);
        return Result.Success(categories);
    }
}
