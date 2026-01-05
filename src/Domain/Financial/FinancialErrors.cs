using SharedKernel;
namespace Domain.Financial;
public static class FinancialErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("Financial.NotFound", $"The transaction with the Id = '{id}' was not found");
    public static Error AttributesInvalid() => Error.Validation("Financial.AttributesInvalid", "Some attributes are invalid or not found");
}
