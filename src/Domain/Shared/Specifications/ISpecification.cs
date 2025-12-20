using System.Linq.Expressions;

namespace Domain.Shared.Specifications;

public interface ISpecification<T>
{
    Expression<Func<T, bool>> ToExpression();
    
    bool IsSatisfiedBy(T entity);
}

