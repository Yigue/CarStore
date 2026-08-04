using Domain.Clients;
using Domain.Clients.Attributes;
using Infrastructure.Persistence.Configurations.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Infrastructure.Persistence.Configurations.Clients;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.DNI)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(c => c.DNI)
            .IsUnique();

        builder.Property(c => c.Email)
            .HasConversion(new EmailValueConverter())
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(c => c.Phone)
            .HasMaxLength(20);

        builder.Property(c => c.Address)
            .HasMaxLength(200);

        builder.Property(c => c.City)
            .HasColumnName("city")
            .HasMaxLength(100);

        builder.Property(c => c.ZipCode)
            .HasColumnName("zip_code")
            .HasMaxLength(20);

        builder.Property(c => c.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        builder.Property(c => c.Status)
            .HasConversion<string>();

        builder.Property(c => c.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(Domain.Clients.Attributes.ClientType.Individual);

        // Soft-delete columns (ADR-2, mirrors Quote pattern)
        builder.Property(c => c.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasIndex(c => c.IsDeleted)
            .HasDatabaseName("ix_clients_is_deleted");

        builder.Property(c => c.DeletedAtUtc)
            .HasColumnName("deleted_at_utc");

        builder.Property(c => c.DeletedBy)
            .HasColumnName("deleted_by");

        // Enrichment columns (REQ-001)
        builder.Property(c => c.AcquisitionSource)
            .HasConversion<string>()
            .HasColumnName("acquisition_source")
            .HasMaxLength(20);

        builder.Property(c => c.AssignedAgentId)
            .HasColumnName("assigned_agent_id");

        builder.Property(c => c.OriginLeadId);

        builder.HasOne<Domain.Leads.Lead>()
            .WithMany()
            .HasForeignKey(c => c.OriginLeadId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasMany(c => c.Sales)
            .WithOne(s => s.Client)
            .HasForeignKey(s => s.ClientId);

        // qa-p0-blockers C1: the `search_name` STORED generated column and its GIN trgm index
        // are Postgres-only and are configured in ApplicationDbContext.OnModelCreating, inside
        // the existing `if (!isSqlite)` block. They cannot live here: this configuration is
        // applied via ApplyConfigurationsFromAssembly for every provider, and both the
        // `f_unaccent(...)` computed SQL and the "C" collation are unknown to SQLite.
    }
}
