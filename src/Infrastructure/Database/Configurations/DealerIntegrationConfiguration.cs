using Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

/// <summary>sfc C1.1 / ADR C22: one connection per (dealer, provider); the credential column holds Data Protection output only.</summary>
internal sealed class DealerIntegrationConfiguration : IEntityTypeConfiguration<DealerIntegration>
{
    public void Configure(EntityTypeBuilder<DealerIntegration> builder)
    {
        builder.ToTable("dealer_integrations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.DealerId).IsRequired();
        builder.Property(i => i.Provider).HasMaxLength(DealerIntegration.MaxProviderLength).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.EncryptedCredentials);
        builder.Property(i => i.Scopes).HasMaxLength(1000).IsRequired();
        builder.Property(i => i.LastError).HasMaxLength(1000);
        builder.Ignore(i => i.DomainEvents);
        builder.HasIndex(i => new { i.DealerId, i.Provider }).IsUnique().HasDatabaseName("ux_dealer_integrations_dealer_id_provider");
    }
}

/// <summary>sfc C1.1 / ADR C22: outbound command queue, polled by the due index.</summary>
internal sealed class IntegrationCommandConfiguration : IEntityTypeConfiguration<IntegrationCommand>
{
    public void Configure(EntityTypeBuilder<IntegrationCommand> builder)
    {
        builder.ToTable("integration_commands");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.DealerId).IsRequired();
        builder.Property(c => c.Provider).HasMaxLength(DealerIntegration.MaxProviderLength).IsRequired();
        builder.Property(c => c.Operation).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Payload).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.LastError).HasMaxLength(IntegrationCommand.MaxErrorLength);
        builder.Ignore(c => c.DomainEvents);
        builder.HasIndex(c => new { c.Status, c.NextAttemptAtUtc }).HasDatabaseName("ix_integration_commands_status_next_attempt_at_utc");
        builder.HasIndex(c => c.DealerId).HasDatabaseName("ix_integration_commands_dealer_id");
    }
}
