using System.Linq.Expressions;
using Domain.Clients;
using Domain.Clients.Attributes;

namespace Domain.Shared.Specifications;

public sealed class ActiveClientsSpecification : ISpecification<Client>
{
    public Expression<Func<Client, bool>> ToExpression()
    {
        return client => client.Status == ClientStatus.Active;
    }
    
    public bool IsSatisfiedBy(Client client)
    {
        return client.Status == ClientStatus.Active;
    }
}

