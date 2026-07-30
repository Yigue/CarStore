using Domain.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Appointment"/>. Snake_case column/table naming is
/// applied automatically via <c>EFCore.NamingConventions</c>, so <c>ToTable()</c> is omitted.
/// </summary>
internal sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.DealerId).IsRequired();
        builder.Property(a => a.VehicleId).IsRequired();
        
        builder.Property(a => a.ClientId)
            .IsRequired(false);
            
        builder.Property(a => a.LeadId)
            .IsRequired(false);
            
        builder.Property(a => a.AgentId).IsRequired();

        builder.Property(a => a.StartDateTime).IsRequired();
        builder.Property(a => a.EndDateTime).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.Property(a => a.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(AppointmentStatus.Scheduled);

        builder.Property(a => a.Notes)
            .HasMaxLength(2000);

        // Foreign keys to Cars / Clients / Users (agent). Restrict deletes so the
        // appointment row blocks accidental cascading deletions of parent records.
        builder.HasOne<Domain.Cars.Car>()
            .WithMany()
            .HasForeignKey(a => a.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Clients.Client>()
            .WithMany()
            .HasForeignKey(a => a.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Leads.Lead>()
            .WithMany()
            .HasForeignKey(a => a.LeadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Users.User>()
            .WithMany()
            .HasForeignKey(a => a.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // CRITICAL: composite index optimizes the time-range overlap query used for
        // conflict detection in CreateAppointmentCommandHandler / RescheduleAppointmentCommandHandler.
        // Filter shape: dealer_id == X AND start_date_time < @to AND end_date_time > @from.
        builder.HasIndex(a => new { a.DealerId, a.StartDateTime, a.EndDateTime })
            .HasDatabaseName("ix_appointments_dealer_time_range");
    }
}
