using SharedKernel;

namespace Domain.Financial.Attributes;

public sealed class TransactionCategory : Entity
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public TransactionType Type { get; private set; }

    public bool IsSystem => DealerId == Guid.Empty;

    private TransactionCategory() { }

    public TransactionCategory(string name, string description, TransactionType type)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        Type = type;
    }

    public TransactionCategory(Guid dealerId, string name, string description, TransactionType type)
    {
        Id = Guid.NewGuid();
        if (dealerId != Guid.Empty)
        {
            SetDealer(dealerId);
        }
        Name = name;
        Description = description;
        Type = type;
    }

    public static TransactionCategory CreateSystem(string name, string description, TransactionType type)
    {
        return new TransactionCategory(name, description, type);
    }

    public void Update(string name, string description, TransactionType type)
    {
        Name = name;
        Description = description;
        Type = type;
    }
}
