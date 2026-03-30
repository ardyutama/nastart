using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Purchases;

namespace Nastart.Api.Shared.Data.Configurations;

public sealed class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        builder.ToTable("purchase_items");
        builder.HasKey(pi => pi.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("uuidv7()");

        builder.HasIndex(pi => pi.IngredientId);

        builder.Property(pi => pi.RawText).HasMaxLength(500);
        builder.Property(pi => pi.Quantity).HasPrecision(18, 4);
        builder.Property(pi => pi.Price).HasPrecision(18, 4);

        builder.Property(pi => pi.Status)
            .IsRequired()
            .HasDefaultValue(ItemStatus.Pending);

        builder.HasOne(pi => pi.Purchase)
            .WithMany(p => p.PurchaseItems)
            .HasForeignKey(pi => pi.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pi => pi.Ingredient)
            .WithMany(pi => pi.PurchaseItems)
            .HasForeignKey(pi => pi.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}