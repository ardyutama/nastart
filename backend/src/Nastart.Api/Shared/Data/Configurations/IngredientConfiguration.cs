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

        builder.Property(i => i.Unit)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.CurrentPrice)
            .HasColumnType("decimal(18,4)");

        builder.Property(e => e.CurrentStock)
            .HasColumnType("decimal(18,4)");

        builder.Property(e => e.MinStock)
            .HasColumnType("decimal(18,4)");

        builder.HasOne(i => i.Category)
            .WithMany(c => c.Ingredients)
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasMany(i => i.PriceHistories)
            .WithOne(ph => ph.Ingredient)
            .HasForeignKey(ph => ph.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}