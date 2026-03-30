using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Shared.Data.Configurations;

public sealed class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.ToTable("ingredients");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(i => i.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(i => i.Name).HasMethod("gin") .HasOperators("gin_trgm_ops");;
        builder.HasIndex(i => i.Userid);

        builder.Property(i => i.Unit)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.CurrentPrice)
            .HasPrecision(18, 4);

        builder.Property(e => e.CurrentStock)
            .HasPrecision(18, 4);

        builder.Property(e => e.MinStock)
            .HasPrecision(18, 4);

        builder.HasOne(i => i.Category)
            .WithMany(c => c.Ingredients)
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.PriceHistories)
            .WithOne(ph => ph.Ingredient)
            .HasForeignKey(ph => ph.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(i => i.PurchaseItems)
            .WithOne(i => i.Ingredient)
            .HasForeignKey(i => i.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(i => i.RecipeIngredients)
            .WithOne(i => i.Ingredient)
            .HasForeignKey(i => i.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}