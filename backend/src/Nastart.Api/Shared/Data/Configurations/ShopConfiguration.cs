using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Shops;

namespace Nastart.Api.Shared.Data.Configurations;

public class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("shops");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("uuidv7()");

        builder.Property(s => s.Name).HasMaxLength(200);

        builder.HasOne(s => s.User)
            .WithMany(s => s.Shops)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Purchases)
            .WithOne(s => s.Shop)
            .HasForeignKey(s => s.ShopId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}