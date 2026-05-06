using System.Text.RegularExpressions;
using SharedKernel;

namespace Domain.Shared.ValueObjects;

public sealed record Email
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Value { get; init; }

    private Email() { } // Para EF Core

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Email cannot be empty");
        
        var normalized = value.Trim().ToLowerInvariant();
        
        if (!EmailRegex.IsMatch(normalized))
            throw new DomainException("Invalid email format");
        
        Value = normalized;
    }

    public static implicit operator string(Email email) => email.Value;
    
    public override string ToString() => Value;
}

