using SharedKernel;

namespace Domain.Financial;

public static class FinancialErrors
{
    public static Error NotFound(Guid id) => Error.NotFound(
        "Financial.NotFound",
        $"The transaction with identity '{id}' was not found");

    public static Error AttributesInvalid() => Error.Validation(
        "Financial.AttributesInvalid",
        "The attributes provided are invalid");

    // D3 (qa-p1-integridad PR2): raised both by DeleteCategoryCommandHandler's pre-check
    // (AnyAsync) and by its DbUpdateException/23503 catch (the TOCTOU race a schema-only
    // RESTRICT closes) — same observable contract either way, 409 never 500.
    public static Error CategoryInUse(int blockingTransactionCount) => Error.Conflict(
        "Category.InUse",
        $"The category cannot be deleted because it is referenced by {blockingTransactionCount} transaction(s).");
}
