using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Shared.Data.Configurations;

public class PriceHistoryConfiguration : IEntityTypeConfiguration<PriceHistory>
{
    public void Configure(EntityTypeBuilder<PriceHistory> builder)
    {
        builder.HasKey(ph => ph.Id);
        builder.Property(ph => ph.Id).HasDefaultValueSql("uuidv7()");

        builder.HasIndex(ph => new { ph.IngredientId, ph.CreatedAt });

        builder.Property(ph => ph.Price).HasPrecision(18, 4);

        builder.HasOne(ph => ph.Ingredient)
            .WithMany(ph => ph.PriceHistories)
            .HasForeignKey(ph => ph.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}