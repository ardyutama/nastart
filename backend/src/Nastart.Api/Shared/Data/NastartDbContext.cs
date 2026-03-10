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
            
        }
        );
    }
}