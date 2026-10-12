using System.Globalization;
using Application.Abstractions.Auditing;
using Application.Abstractions.Authentication;
using Domain.Auditing;
using Domain.Cars;
using Domain.Deposits;
using Domain.Sales;
using Domain.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using SharedKernel;

namespace Infrastructure.Database.Interceptors;

/// <summary>
/// sfc B2.2 / ADR C10 / RN-025: for every MODIFIED, allow-listed property it adds a <see cref="DealerAuditLogEntry"/> to the
/// SAME unit of work (it runs inside <c>SaveChanges</c>, so the entry commits or rolls back with the change). Actor comes
/// from <see cref="IUserContext"/> (the system actor only inside an explicit AsSystem scope; otherwise a missing user fails the save), the business reason
/// from <see cref="IAuditContext.Reason"/>. Inserts and deletes are not audited here; a change that bypasses the change
/// tracker (<c>ExecuteUpdate</c>/<c>ExecuteDelete</c>) is invisible to it, which an architecture ratchet forbids.
/// </summary>
public sealed class TenantAuditInterceptor(
    IUserContext userContext,
    IAuditContext auditContext,
    IDateTimeProvider clock) : SaveChangesInterceptor
{
    private const string Wildcard = "*";

    /// <summary>Allow-list: entity type to audited property names (<c>*</c> = every property).</summary>
    private static readonly Dictionary<Type, string[]> Allowed = new()
    {
        [typeof(Car)] = [nameof(Car.Price)],
        [typeof(Sale)] = [nameof(Sale.Status)],
        // Deposit is not mapped in ApplicationDbContext yet (B1.3 wiring pending): the entry is inert until it is.
        [typeof(Deposit)] = [Wildcard],
    };

    public static IReadOnlySet<Type> AuditedEntityTypes { get; } = Allowed.Keys.ToHashSet();

    public static bool IsAudited(Type entityType, string property) =>
        Allowed.TryGetValue(entityType, out string[]? props)
        && (props.Contains(Wildcard, StringComparer.Ordinal) || props.Contains(property, StringComparer.Ordinal));

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        auditContext.ClearReason();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        auditContext.ClearReason();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        auditContext.ClearReason();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        auditContext.ClearReason();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null)
            return;

        GuardAppendOnly(context);

        context.ChangeTracker.DetectChanges();
        var changes = new List<(Entity Owner, string Property, string? Old, string? New)>();

        foreach (EntityEntry entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Metadata.IsOwned())
            {
                CollectOwned(entry, context, changes);
            }
            else if (entry.State == EntityState.Modified
                     && entry.Entity is Entity entity
                     && Allowed.ContainsKey(entry.Entity.GetType()))
            {
                foreach (PropertyEntry property in entry.Properties.Where(p => p.IsModified))
                {
                    string name = property.Metadata.Name;
                    if (!IsAudited(entry.Entity.GetType(), name) || Equals(property.OriginalValue, property.CurrentValue))
                        continue;

                    changes.Add((entity, name, Format(property.OriginalValue), Format(property.CurrentValue)));
                }
            }
        }

        if (changes.Count == 0)
            return;

        DateTime now = clock.UtcNow;
        Guid actor = ResolveActor();
        foreach ((Entity owner, string property, string? oldValue, string? newValue) in changes)
        {
            context.Set<DealerAuditLogEntry>().Add(DealerAuditLogEntry.Record(
                owner.DealerId, owner.GetType().Name, owner.Id, property, oldValue, newValue,
                actor, auditContext.Reason, auditContext.CorrelationId, now));
        }
    }

    /// <summary>
    /// An owned <see cref="Money"/> (e.g. <c>Car.Price</c>) is a separate tracked entry mapped to the owner's row. Replacing
    /// the instance (<c>Price = Money.Of(...)</c>) can leave the owner Unchanged, so owned entries are inspected on their own.
    /// </summary>
    private static void CollectOwned(
        EntityEntry owned, DbContext context, List<(Entity Owner, string Property, string? Old, string? New)> changes)
    {
        IForeignKey? ownership = owned.Metadata.FindOwnership();
        string? navigation = ownership?.PrincipalToDependent?.Name;
        Type? ownerType = ownership?.PrincipalEntityType.ClrType;
        if (ownership is null || navigation is null || ownerType is null
            || !IsAudited(ownerType, navigation) || owned.Entity is not Money)
        {
            return;
        }

        // Only the entry carrying the NEW value drives the diff (Modified in place, or Added after a replacement).
        if (owned.State is not (EntityState.Modified or EntityState.Added))
            return;

        EntityEntry? ownerEntry = context.ChangeTracker.Entries()
            .FirstOrDefault(e => e.Entity.GetType() == ownerType && OwnsEntry(e, navigation, owned));
        if (ownerEntry?.Entity is not Entity ownerEntity || ownerEntry.State == EntityState.Added)
            return;

        EntityEntry? replaced = context.ChangeTracker.Entries()
            .FirstOrDefault(e => e.Entity is Money && e.State == EntityState.Deleted
                && e.Metadata == owned.Metadata && Equals(OwnerKeyOf(e), ownerEntity.Id));

        string? oldValue = replaced is not null
            ? Format(replaced.Entity)
            : owned.State == EntityState.Modified ? FormatOriginal(owned) : null;
        string? newValue = Format(owned.Entity);

        if (oldValue != newValue)
            changes.Add((ownerEntity, navigation, oldValue, newValue));
    }

    private static bool OwnsEntry(EntityEntry owner, string navigation, EntityEntry owned) =>
        ReferenceEquals(owner.Reference(navigation).CurrentValue, owned.Entity);

    /// <summary>The owner's id as stored in the owned entry's foreign key (owned entries share the owner's key).</summary>
    private static object? OwnerKeyOf(EntityEntry owned) =>
        owned.Property(owned.Metadata.FindOwnership()!.Properties[0].Name).CurrentValue;

    private static string? FormatOriginal(EntityEntry owned) =>
        $"{FormatAmount((decimal)owned.Property(nameof(Money.Amount)).OriginalValue!)} {owned.Property(nameof(Money.Currency)).OriginalValue}";

    /// <summary>
    /// The signed-in user when there is one. The system actor ONLY inside an explicit <see cref="IAuditContext.AsSystem"/>
    /// scope; otherwise a missing user context fails the save instead of recording the write as a system one.
    /// </summary>
    private Guid ResolveActor()
    {
        try
        {
            return userContext.UserId;
        }
        catch (ApplicationException ex)
        {
            if (auditContext.IsSystem)
                return DealerAuditLogEntry.SystemActorId;

            throw new InvalidOperationException(
                "An audited change was saved without a user context and outside an explicit system scope "
                + "(IAuditContext.AsSystem()); refusing to record it as a system write.", ex);
        }
    }

    /// <summary>The audit log is append-only: nothing may update or delete an existing entry through the tracker.</summary>
    private static void GuardAppendOnly(DbContext context)
    {
        if (context.ChangeTracker.Entries<DealerAuditLogEntry>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("DealerAuditLogEntry is append-only and cannot be modified or deleted.");
        }
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        Money money => $"{FormatAmount(money.Amount)} {money.Currency}",
        decimal amount => FormatAmount(amount),
        DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture),
    };

    private static string FormatAmount(decimal amount) => amount.ToString("0.##", CultureInfo.InvariantCulture);
}
