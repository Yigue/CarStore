using Domain.Cars.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations.Cars;

public class MarcaConfiguration : IEntityTypeConfiguration<Marca>
{
    public void Configure(EntityTypeBuilder<Marca> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Nombre)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(m => m.NombreNormalizado)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(m => m.NombreNormalizado)
            .IsUnique()
            .HasDatabaseName("ix_marca_nombre_normalizado");

        builder.HasMany(m => m.Modelos)
            .WithOne(mo => mo.Marca)
            .HasForeignKey(mo => mo.MarcaId);
    }
}
