using Domain.Cars.Atribbutes;
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

        builder.HasMany(m => m.Modelos)
            .WithOne(mo => mo.Marca)
            .HasForeignKey(mo => mo.MarcaId);

     
    }
}
