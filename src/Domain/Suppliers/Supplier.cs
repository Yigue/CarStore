using SharedKernel;

namespace Domain.Suppliers;

/// <summary>sfc B7.4 / RN-039: who the dealer buys vehicles from. The name is unique per dealer regardless of case.</summary>
public sealed class Supplier : Entity
{
    public const int MaxNameLength = 200;

    public string Name { get; private set; } = string.Empty;
    public string NameNormalized { get; private set; } = string.Empty;
    public string? ContactEmail { get; private set; }
    public string? Phone { get; private set; }

    private Supplier() { }

    public static Result<Supplier> Create(Guid dealerId, string name, string? contactEmail, string? phone)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > MaxNameLength)
            return Result.Failure<Supplier>(Error.Validation("Supplier.NameInvalid", "El nombre del proveedor es requerido (máx. 200 caracteres)."));

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = trimmed,
            NameNormalized = Normalize(trimmed),
            ContactEmail = string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
        };
        supplier.SetDealer(dealerId);
        return supplier;
    }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();
}

public static class SupplierErrors
{
    public static Error AlreadyExists(string name) => Error.Conflict("Supplier.AlreadyExists", $"Ya existe un proveedor con el nombre '{name}'.");
    public static Error NotFound(Guid id) => Error.NotFound("Supplier.NotFound", $"El proveedor '{id}' no existe.");
}
