namespace Domain.Cars;

/// <summary>
/// Lifecycle state of a <see cref="ReconditioningTask"/>.
/// </summary>
public enum ReconditioningStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2
}
