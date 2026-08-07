using SharedKernel;

namespace Domain.Shared.ValueObjects;

public sealed record Money : IComparable<Money>
{
    public decimal Amount { get; init; }
    public string Currency { get; init; }

    private Money() { } // Para EF Core

#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
    public Money(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new DomainException("Money amount cannot be negative");
        
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero => new(0);
    
    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new DomainException("Cannot add money with different currencies");
        
        return new Money(left.Amount + right.Amount, left.Currency);
    }
    
    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new DomainException("Cannot subtract money with different currencies");
        
        return new Money(left.Amount - right.Amount, left.Currency);
    }
    
    public static Money operator *(Money money, decimal multiplier)
    {
        return new Money(money.Amount * multiplier, money.Currency);
    }
    
    public static bool operator >(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new DomainException("Cannot compare money with different currencies");
        
        return left.Amount > right.Amount;
    }
    
    public static bool operator <(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new DomainException("Cannot compare money with different currencies");
        
        return left.Amount < right.Amount;
    }
    
    public static bool operator >=(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new DomainException("Cannot compare money with different currencies");
        
        return left.Amount >= right.Amount;
    }
    
    public static bool operator <=(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new DomainException("Cannot compare money with different currencies");

        return left.Amount <= right.Amount;
    }

    /// <summary>
    /// Orders by <see cref="Amount"/>, consistent with the comparison operators above.
    /// Required so callers can order a sequence of Money (for example the in-memory
    /// EF provider, which evaluates OrderBy client-side; relational providers translate
    /// the same expression to ORDER BY on the converted column and never reach here).
    /// A null instance sorts first, matching the framework convention for reference types.
    /// </summary>
    public int CompareTo(Money? other)
    {
        if (other is null) return 1;

        if (Currency != other.Currency)
            throw new DomainException("Cannot compare money with different currencies");

        return Amount.CompareTo(other.Amount);
    }
}

