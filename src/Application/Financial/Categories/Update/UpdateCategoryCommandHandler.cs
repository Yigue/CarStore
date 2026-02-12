using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Financial.Attributes;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Financial.Categories.Update;

internal sealed class UpdateCategoryCommandHandler(IApplicationDbContext context)
    : ICommandHandler<UpdateCategoryCommand>
{
    public async Task<Result> Handle(UpdateCategoryCommand command, CancellationToken cancellationToken)
    {
        var category = await context.TransactionCategories
            .FirstOrDefaultAsync(c => c.Id == command.Id, cancellationToken);

        if (category is null)
        {
            return Result.Failure(Error.NotFound("Category.NotFound", "The category was not found."));
        }

        category.Update(
            command.Name,
            command.Description,
            command.Type);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
