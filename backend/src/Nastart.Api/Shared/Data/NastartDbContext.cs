using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Shared.Data;

public class NastartDbContext(DbContextOptions<NastartDbContext> options)
    : DbContext(options)
{
    public DbSet<Ingredient> Ingredients {get; set;}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(e => e.Unit)
                .HasMaxLength(50);
            entity.Property(e => e.CurrentPrice)
                .HasPrecision(18,4);
            entity.Property(e => e.CurrentStock)
                .HasPrecision(18,4);
            entity.Property(e => e.MinStock)
                .HasPrecision(18,4);
        }
        );
    }
}