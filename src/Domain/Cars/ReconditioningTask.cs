using Domain.Cars.Events;
using Domain.Shared.ValueObjects;
using SharedKernel;

namespace Domain.Cars;

/// <summary>
/// A single reconditioning work-item performed on a <see cref="Car"/> before it is sold
/// (e.g. detailing, brake replacement, paint touch-up). Belongs to the Car aggregate.
/// </summary>
public sealed class ReconditioningTask : Entity
{
    public Guid CarId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Money Cost { get; private set; } = Money.Zero;
    public ReconditioningStatus Status { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // EF Core
    private ReconditioningTask() { }

    private ReconditioningTask(Guid dealerId, Guid carId, string description, Money cost)
    {
        SetDealer(dealerId);
        Id = Guid.NewGuid();
        CarId = carId;
        Description = description;
        Cost = cost;
        Status = ReconditioningStatus.Pending;
        CompletedAt = null;
    }

    /// <summary>
    /// Factory method. The parent <see cref="Car"/> is responsible for invoking this
    /// (so DealerId propagation and aggregate invariants are enforced upstream).
    /// </summary>
    public static ReconditioningTask Create(Guid dealerId, Guid carId, string description, Money cost)
    {
        if (carId == Guid.Empty)
            throw new DomainException("CarId cannot be empty");

        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("ReconditioningTask description is required");

        ArgumentNullException.ThrowIfNull(cost);

        return new ReconditioningTask(dealerId, carId, description, cost);
    }

    /// <summary>
    /// Transitions this task to <see cref="ReconditioningStatus.Completed"/> and raises
    /// <see cref="ReconditioningTaskCompletedDomainEvent"/>. Idempotent: a no-op if already completed.
    /// </summary>
    public void Complete(DateTime completedAtUtc)
    {
        if (Status == ReconditioningStatus.Completed)
            return;

        Status = ReconditioningStatus.Completed;
        CompletedAt = completedAtUtc;

        Raise(new ReconditioningTaskCompletedDomainEvent(
            CarId,
            Id,
            Cost.Amount,
            Cost.Currency,
            completedAtUtc));
    }

    /// <summary>
    /// Optional state useful when work has started but is not yet finished.
    /// </summary>
    public void Start()
    {
        if (Status == ReconditioningStatus.Completed)
            throw new DomainException("Cannot start a completed reconditioning task");

        Status = ReconditioningStatus.InProgress;
    }
}
