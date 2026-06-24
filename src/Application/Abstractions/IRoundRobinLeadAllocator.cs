namespace Application.Abstractions;

public interface IRoundRobinLeadAllocator
{
    Task<Guid?> AllocateAsync(Guid dealerId, CancellationToken ct);
}
