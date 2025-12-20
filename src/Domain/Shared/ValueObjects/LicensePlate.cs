using System.Text.RegularExpressions;
using SharedKernel;

namespace Domain.Shared.ValueObjects;

public sealed record LicensePlate
{
    // Regex para patentes argentinas (formato: ABC123 o AB123CD)
    private static readonly Regex LicensePlateRegex = new(
        @"^[A-Z]{2,3}\d{3}[A-Z]{0,2}$",
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

