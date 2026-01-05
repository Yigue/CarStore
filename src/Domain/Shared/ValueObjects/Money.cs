using SharedKernel;

namespace Domain.Shared.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; }

    private Money() { } // Para EF Core

    public Money(decimal amount, string currency = "USD")
    {
        if (amount < 0)
        {
            throw new DomainException("Money amount cannot be negative");
        }
        
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero => new(0);
    
    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new DomainException("Cannot add money with different currencies");
        }
        
        return new Money(left.Amount + right.Amount, left.Currency);
    }
    
    public static Money operator -(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new DomainException("Cannot subtract money with different currencies");
        }
        
        return new Money(left.Amount - right.Amount, left.Currency);
    }
    
    public static Money operator *(Money money, decimal multiplier) => new Money(money.Amount * multiplier, money.Currency);

#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations
    public static bool operator >(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
#pragma warning disable S3877 // Exceptions should not be thrown from unexpected methods
            throw new InvalidOperationException("Cannot compare money with different currencies");
#pragma warning restore S3877 // Exceptions should not be thrown from unexpected methods
        }
        
        return left.Amount > right.Amount;
    }
    
    public static bool operator <(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
#pragma warning disable S3877 // Exceptions should not be thrown from unexpected methods
            throw new InvalidOperationException("Cannot compare money with different currencies");
#pragma warning restore S3877 // Exceptions should not be thrown from unexpected methods
        }
        
        return left.Amount < right.Amount;
    }
    
    public static bool operator >=(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
#pragma warning disable S3877 // Exceptions should not be thrown from unexpected methods
            throw new InvalidOperationException("Cannot compare money with different currencies");
#pragma warning restore S3877 // Exceptions should not be thrown from unexpected methods
        }
        
        return left.Amount >= right.Amount;
    }
    
    public static bool operator <=(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
#pragma warning disable S3877 // Exceptions should not be thrown from unexpected methods
            throw new InvalidOperationException("Cannot compare money with different currencies");
#pragma warning restore S3877 // Exceptions should not be thrown from unexpected methods
        }
        
        return left.Amount <= right.Amount;
    }
#pragma warning restore CA1065 // Do not raise exceptions in unexpected locations
}
