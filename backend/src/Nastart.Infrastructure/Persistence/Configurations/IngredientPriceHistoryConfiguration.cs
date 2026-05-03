using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Domain.Entities;

namespace Nastart.Infrastructure.Persistence.Configurations;

public class IngredientPriceHistoryConfiguration : IEntityTypeConfiguration<IngredientPriceHistory>
{
    public void Configure(EntityTypeBuilder<IngredientPriceHistory> builder)
    {
        builder.Property(p => p.Price).HasPrecision(10, 4);
        builder.Property(p => p.UnitSize).HasPrecision(10, 4);

        builder.Property(p => p.Source).HasConversion<string>().HasMaxLength(20);

        builder.Property(p => p.CommitedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(p => new { p.IngredientId, p.CommitedAt })
            .IsDescending(false, true);

        builder.HasOne(p => p.Ingredient)
            .WithMany(i => i.PriceHistory)
            .HasForeignKey(p => p.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}