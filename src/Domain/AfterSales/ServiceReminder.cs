using SharedKernel;

namespace Domain.AfterSales;

public enum ServiceReminderKind
{
    PeriodicService,
    WarrantyExpiring,
}

/// <summary>
/// sfc C2.4 / RN-049: a reminder for the dealer about a delivered sale. <see cref="ClientContactAllowed"/> records
/// whether the client consented to be contacted for it when it was created.
/// </summary>
public sealed class ServiceReminder : Entity
{
    public Guid SaleId { get; private set; }
    public Guid ClientId { get; private set; }
    public ServiceReminderKind Kind { get; private set; }
    public DateTime DueOnUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public bool ClientContactAllowed { get; private set; }

    private ServiceReminder() { }

    public static ServiceReminder Create(
        Guid dealerId, Guid saleId, Guid clientId, ServiceReminderKind kind, DateTime dueOnUtc, bool clientContactAllowed, DateTime nowUtc)
    {
        var reminder = new ServiceReminder
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ClientId = clientId,
            Kind = kind,
            DueOnUtc = dueOnUtc,
            CreatedAtUtc = nowUtc,
            ClientContactAllowed = clientContactAllowed,
        };
        reminder.SetDealer(dealerId);
        return reminder;
    }
}
