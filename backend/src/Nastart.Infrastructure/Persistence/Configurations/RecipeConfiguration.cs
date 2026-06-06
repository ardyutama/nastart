using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Domain.Entities;

namespace Nastart.Infrastructure.Persistence.Configurations;

public sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> entity)
    {
        entity.HasKey(r => r.Id);

        entity.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        entity.HasIndex(r => new { r.VersionGroupId, r.VersionLabel })
            .IsUnique();

        entity.Property(r => r.CostPerPortion)
            .HasPrecision(18, 4);

        entity.Property(r => r.PackagingCost)
            .HasPrecision(10, 4)
            .HasDefaultValue(0m);

        entity.Property(r => r.TargetMargin)
            .HasPrecision(5, 4)
            .HasDefaultValue(0m);

        entity.HasIndex(r => new { r.VersionGroupId, r.VersionNumber })
            .IsDescending(false, true);

        entity.HasIndex(r => new { r.UserId, r.VersionGroupId });

        entity.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(r => r.RecipeItems)
            .WithOne(ri => ri.Recipe)
            .HasForeignKey(ri => ri.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}