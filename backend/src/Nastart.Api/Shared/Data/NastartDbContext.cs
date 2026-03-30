using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Features.Purchases;
using Nastart.Api.Features.Recipes;
using Nastart.Api.Features.Shops;

namespace Nastart.Api.Shared.Data;

public class NastartDbContext(DbContextOptions<NastartDbContext> options)
    : DbContext(options)
{
    public DbSet<Ingredient> Ingredients { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<PriceHistory> PriceHistories {get; set; }
    public DbSet<Purchase> Purchases { get; set; }
    public DbSet<PurchaseItem> PurchaseItems { get; set; }
    public DbSet<Recipe> Recipes {get; set; }
    public DbSet<Shop> Shops { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NastartDbContext).Assembly);
    }
}