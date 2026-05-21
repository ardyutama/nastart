using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Domain.Common;

namespace Nastart.Infrastructure.Persistence.Configurations;

public sealed class RecipeItemConfiguration : IEntityTypeConfiguration<RecipeItem>
{
    public void Configure(EntityTypeBuilder<RecipeItem> entity)
    {
        entity.HasKey(ri => ri.Id);

        entity.Property(ri => ri.Quantity)
            .HasPrecision(18, 4);
        
        entity.Property(ri => ri.YieldPercentage)
            .HasPrecision(5, 4);
        
        entity.HasOne(ri => ri.Ingredient)
            .WithMany()
            .HasForeignKey(ri => ri.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(ri => ri.IngredientId);
    }
}