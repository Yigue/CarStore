using SharedKernel;
namespace Domain.Financial.Attributes;
public sealed class TransactionCategory : Entity
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public TransactionType Type { get; private set; }
    private TransactionCategory() { }
    public TransactionCategory(string name, string description, TransactionType type)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        Type = type;
    }
}
