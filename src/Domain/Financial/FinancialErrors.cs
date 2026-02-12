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
}
