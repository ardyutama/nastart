using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Domain.Entities;

namespace Nastart.Infrastructure.Persistence.Configurations;

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.Property(u => u.Name).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Abbrevation).HasMaxLength(20).IsRequired();

        builder.HasIndex(u => u.Name).IsUnique()
            .HasDatabaseName("units_name_idx");
        builder.HasIndex(u => u.Abbrevation).IsUnique()
            .HasDatabaseName("units_abbreviation_idx");
    }
}