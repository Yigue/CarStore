using System.Linq.Expressions;
using Domain.Quotes;
using Domain.Quotes.Attributes;
using SharedKernel;

namespace Domain.Shared.Specifications;

public sealed class ExpiredQuotesSpecification : ISpecification<Quote>
{
    private readonly IDateTimeProvider _dateTimeProvider;
    
    public ExpiredQuotesSpecification(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }
    
    public Expression<Func<Quote, bool>> ToExpression()
    {
        var now = _dateTimeProvider.UtcNow;
        return quote => quote.Status == QuoteStatus.Pending 
                   && quote.ValidUntil < now;
    }
    
    public bool IsSatisfiedBy(Quote quote)
    {
        return quote.Status == QuoteStatus.Pending 
            && quote.ValidUntil < _dateTimeProvider.UtcNow;
    }
}

