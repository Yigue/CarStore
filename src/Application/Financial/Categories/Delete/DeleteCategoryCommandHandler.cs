using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Financial.Categories.Delete;

internal sealed class DeleteCategoryCommandHandler(IApplicationDbContext context)
    : ICommandHandler<DeleteCategoryCommand>
{
    public async Task<Result> Handle(DeleteCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = await context.TransactionCategories
            .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);

        if (category is null)
        {
            return Result.Failure(Error.NotFound("Category.NotFound", "The category was not found."));
        }

        context.TransactionCategories.Remove(category);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
