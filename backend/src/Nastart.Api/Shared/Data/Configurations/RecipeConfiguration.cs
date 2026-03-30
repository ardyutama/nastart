using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Recipes;

namespace Nastart.Api.Shared.Data.Configurations;

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("recipes");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("uuidv7()");

        builder.HasIndex(r => r.UserId);

        builder.Property(r => r.Name).HasMaxLength(500);
        builder.Property(r => r.SellPrice).HasPrecision(18, 4);
        builder.Property(r => r.TotalCost).HasPrecision(18, 4);
        builder.Property(r => r.MarginPercent).HasPrecision(18, 4);
        builder.Property(r => r.YieldQuantity).HasPrecision(18, 4);
        builder.Property(r => r.YieldUnit).HasPrecision(18, 4);
        builder.Property(r => r.Status).HasMaxLength(200);

        builder.HasOne(r => r.User)
            .WithMany(r => r.Recipes)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        
        builder.HasMany(r => r.RecipeIngredients)
            .WithOne(r => r.Recipe)
            .HasForeignKey(r => r.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}