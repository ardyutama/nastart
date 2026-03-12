using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Purchases;

namespace Nastart.Api.Shared.Data.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("purchases");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(p => p.TotalAmount).HasPrecision(18,4);
        builder.Property(p => p.ReceiptImageUrl).HasMaxLength(500);
        builder.Property(p => p.Notes).HasMaxLength(200);

        builder.HasMany(p => p.Items)
            .WithOne(p => p.Purchase)
            .HasForeignKey(p => p.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.User)
            .WithMany(p => p.Purchases)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}