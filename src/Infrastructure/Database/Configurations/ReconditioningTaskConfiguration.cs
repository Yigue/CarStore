using Domain.Cars;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

// PHASE-4: Money is mapped via OwnsOne (cost_amount + cost_currency) instead of the
// existing MoneyValueConverter pattern (single decimal column, hard-coded "USD")
// because ReconditioningTaskCompletedDomainEvent carries Currency to the ledger
// and we must persist it faithfully.
internal sealed class ReconditioningTaskConfiguration : IEntityTypeConfiguration<ReconditioningTask>
{
    public void Configure(EntityTypeBuilder<ReconditioningTask> builder)
    {
        builder.ToTable("reconditioning_tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.CarId)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.CompletedAt)
            .IsRequired(false);

        builder.OwnsOne(t => t.Cost, cost =>
        {
            cost.Property(c => c.Amount)
                .HasColumnName("cost_amount")
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            cost.Property(c => c.Currency)
                .HasColumnName("cost_currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Navigation(t => t.Cost).IsRequired();

        // PHASE-4: Bind to the public navigation on Car. CarConfiguration sets
        // PropertyAccessMode.Field so EF reads/writes the private _reconditioningTasks
        // list while consumers see the IReadOnlyList<ReconditioningTask>.
        builder.HasOne<Car>()
            .WithMany(nameof(Car.ReconditioningTasks))
            .HasForeignKey(t => t.CarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.CarId);
        builder.HasIndex(t => t.DealerId);
    }
}
