using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;
using SharedKernel;

namespace Application.Financial.Categories.Create;

internal sealed class CreateCategoryCommandHandler(
    IApplicationDbContext context,
    Application.Abstractions.Caching.ICachedCategoryService cachedCategoryService)
    : ICommandHandler<CreateCategoryCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = new TransactionCategory(
            command.Name,
            command.Description,
            command.Type);

        context.TransactionCategories.Add(category);

        await context.SaveChangesAsync(cancellationToken);

        await cachedCategoryService.InvalidateCacheAsync(cancellationToken);

        return Result.Success(category.Id);
    }
}
