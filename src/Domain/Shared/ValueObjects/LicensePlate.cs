using System.Text.RegularExpressions;
using SharedKernel;

namespace Domain.Shared.ValueObjects;

public sealed record LicensePlate
{
    // Regex para patentes argentinas:
    // - Autos Antiguos (1995): AAA123 (3 letras, 3 números)
    // - Autos Mercosur (2016): AA123BB (2 letras, 3 números, 2 letras)
    // - Motos Antiguas: 123ABC (3 números, 3 letras)
    // - Motos Mercosur: A123BCD (1 letra, 3 números, 3 letras)
    // - Otros/Especiales: ABC1234 (3 letras, 4 números)
    private static readonly Regex LicensePlateRegex = new(
        @"^([A-Z]{3}\d{3,4}|[A-Z]{2}\d{3}[A-Z]{2}|[A-Z]\d{3}[A-Z]{3}|\d{3}[A-Z]{3})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; init; }

    private LicensePlate() { } // Para EF Core

    public LicensePlate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("License plate cannot be empty");
        
        var normalized = value.Trim().ToUpperInvariant().Replace(" ", "").Replace("-", "");
        
        if (normalized.Length < 5 || normalized.Length > 8)
            throw new DomainException("License plate must be between 5 and 8 characters");
        
        if (!LicensePlateRegex.IsMatch(normalized))
            throw new DomainException("Invalid license plate format");
        
        Value = normalized;
    }

    public static implicit operator string(LicensePlate licensePlate) => licensePlate.Value;
    
    public override string ToString() => Value;
}

