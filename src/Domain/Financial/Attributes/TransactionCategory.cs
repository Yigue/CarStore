using SharedKernel;

namespace Domain.Financial.Attributes;

public class TransactionCategory : Entity
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public TransactionType Type { get; private set; }
    public bool IsActive { get; private set; }
    
    private TransactionCategory() { }
    
    public TransactionCategory(
        string name,
        string description,
        TransactionType type)
    {
        Name = name;
        Description = description;
        Type = type;
        IsActive = true;
    }
    
    public void Deactivate()
    {
        IsActive = false;
    }
    
    public void Update(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
