using SharedKernel;

namespace Domain.Branches;

/// <summary>
/// sfc C2.1 / RN-048 / ADR C26: a branch of a dealer. The tenant is still the dealer (<c>DealerId</c>); a branch is a
/// data scope inside it. The name is unique per dealer regardless of case.
/// </summary>
public sealed class Branch : Entity
{
    public const int MaxNameLength = 120;

    public string Name { get; private set; } = string.Empty;
    public string NameNormalized { get; private set; } = string.Empty;
    public bool IsMain { get; private set; }

    private Branch() { }

    public static Result<Branch> Create(Guid dealerId, string name, bool isMain)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > MaxNameLength)
            return Result.Failure<Branch>(Error.Validation("Branch.NameInvalid", "El nombre de la sucursal es requerido (máx. 120 caracteres)."));

        var branch = new Branch { Id = Guid.NewGuid(), Name = trimmed, NameNormalized = trimmed.ToUpperInvariant(), IsMain = isMain };
        branch.SetDealer(dealerId);
        return branch;
    }
}

/// <summary>sfc C2.1 / ADR C26: a user working at a branch (used by the branch scope of C2.2).</summary>
public sealed class UserBranch
{
    public Guid UserId { get; private set; }
    public Guid BranchId { get; private set; }

    private UserBranch() { }

    public UserBranch(Guid userId, Guid branchId)
    {
        UserId = userId;
        BranchId = branchId;
    }
}

public static class BranchErrors
{
    public static Error AlreadyExists(string name) => Error.Conflict("Branch.AlreadyExists", $"Ya existe una sucursal '{name}'.");
    public static Error NotFound(Guid id) => Error.NotFound("Branch.NotFound", $"La sucursal '{id}' no existe.");
}
