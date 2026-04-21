using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Domain.Entities;

namespace Nastart.Infrastructure.Persistence.Configurations;

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.HasIndex(i => new { i.UserId, i.Name }).IsUnique()
            .HasDatabaseName("ingredients_user_id_name_idx");

        builder.Property(i => i.Name).HasMaxLength(255).IsRequired();
        builder.Property(i => i.UnitSize).HasPrecision(10, 4);
        builder.Property(i => i.PriceSpikeThresholdPct).HasPrecision(5, 2);

        builder.HasOne(i => i.User)
            .WithMany(u => u.Ingredients)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Category)
            .WithMany(c => c.Ingredients)
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => i.CategoryId)
            .HasDatabaseName("ingredients_category_id_idx");

        builder.HasOne(i => i.Unit)
            .WithMany()
            .HasForeignKey(i => i.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.UnitId)
            .HasDatabaseName("ingredients_unit_id_idx");
    }
}