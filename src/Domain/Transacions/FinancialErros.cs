using SharedKernel;

namespace Domain.Financial;

public static class FinancialErrors
{
    public static Error NotFound(Guid financialId) => Error.NotFound(
        "Financial.NotFound",
        $"The financial with the Id = '{financialId}' was not found");
    public static Error AttributesInvalid() => Error.NotFound(
        "Financial.AttributesInvalid",
        $"Atributes are invalid");
}

