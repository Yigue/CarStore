using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;
using SharedKernel;

namespace Application.Financial.Categories.Create;

internal sealed class CreateCategoryCommandHandler(
    IApplicationDbContext context,
    Application.Abstractions.Caching.ICachedCategoryService cachedCategoryService,
    Application.Abstractions.Tenancy.ICurrentTenantService? tenantService = null)
    : ICommandHandler<CreateCategoryCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCategoryCommand command, CancellationToken cancellationToken)
    {
        var dealerId = tenantService?.HasTenant == true ? tenantService.DealerId : Guid.Empty;
        var category = dealerId != Guid.Empty
            ? new TransactionCategory(dealerId, command.Name, command.Description, command.Type)
            : new TransactionCategory(command.Name, command.Description, command.Type);

        context.TransactionCategories.Add(category);

        await context.SaveChangesAsync(cancellationToken);

        await cachedCategoryService.InvalidateCacheAsync(cancellationToken);

        return Result.Success(category.Id);
    }
}
