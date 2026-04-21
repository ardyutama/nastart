using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Domain.Entities;
using Nastart.Domain.Enums;

namespace Nastart.Infrastructure.Persistence.Configurations;

public class TelegramLinkConfiguration : IEntityTypeConfiguration<TelegramLink>
{
    public void Configure(EntityTypeBuilder<TelegramLink> builder)
    {
        builder.Property(t => t.CodeHash).HasMaxLength(64).IsRequired();

        builder.Property(t => t.TelegramUserId).HasColumnName("telegram_user_id");
        builder.Property(t => t.TelegramUsername).HasMaxLength(255);

        builder.Property(t => t.Status)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => (TelegramLinkStatus)Enum.Parse(typeof(TelegramLinkStatus), v, ignoreCase: true))
            .HasMaxLength(20);

        builder.HasIndex(t => t.CodeHash).IsUnique()
            .HasDatabaseName("telegram_link_code_hash_idx");
        builder.HasIndex(t => new { t.UserId, t.Status })
            .HasDatabaseName("telegram_links_user_id_status_idx");

        builder.HasOne(t => t.User)
            .WithMany(u => u.TelegramLinks)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}