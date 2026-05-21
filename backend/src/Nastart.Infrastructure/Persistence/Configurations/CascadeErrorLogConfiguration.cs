using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Domain.Entities;

namespace Nastart.Infrastructure.Persistence.Configurations;

public sealed class CascadeErrorLogConfiguration : IEntityTypeConfiguration<CascadeErrorLog>
{
    public void Configure(EntityTypeBuilder<CascadeErrorLog> entity)
    {
        entity.HasKey(c => c.Id);

        entity.Property(c => c.ErrorMessage)
            .HasMaxLength(2000)
            .IsRequired();
        
        entity.HasIndex(c => c.IngredientId);

        entity.HasIndex(c => new { c.RecipeId, c.CreatedAt })
            .IsDescending(false, true);
    }
}