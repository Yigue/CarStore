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
        {
            throw new DomainException("Email cannot be empty");
        }

        if (!EmailRegex.IsMatch(value))
        {
            throw new DomainException("Invalid email format");
        }

#pragma warning disable CA1308 // Normalize strings to uppercase
        Value = value.Trim().ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase
    }

    public static implicit operator string(Email email) => email.Value;
    
    public override string ToString() => Value;
}
