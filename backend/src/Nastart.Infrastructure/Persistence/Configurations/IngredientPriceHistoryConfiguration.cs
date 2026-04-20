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

        builder.HasIndex(p => new {p.IngredientId, p.CommitedAt})
            .IsDescending(false, true)
            .HasDatabaseName("ix_ingredient_price_history_ingredient_commited");

        builder.HasOne(p => p.Ingredient)
            .WithMany(i => i.PriceHistory)
            .HasForeignKey(p => p.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(p => p.InvoiceLineItemId)
            .HasFilter("invoice_line_item_id IS NOT NULL")
            .HasDatabaseName("ingredient_price_histories_invoice_line_item_id_idx");
        
        builder.ToTable(t => t.HasCheckConstraint("ck_ingredient_price_history_price_positive", "price > 0"));
        builder.ToTable(t => t.HasCheckConstraint("ck_ingredient_price_history_unit_size_positive", "unit_size > 0"));
    }
}