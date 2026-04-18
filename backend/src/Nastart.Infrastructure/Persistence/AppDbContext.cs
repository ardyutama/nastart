using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;
using Nastart.Domain.Common;
using Nastart.Domain.Entities;

namespace Nastart.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options){ }

    public DbSet<User> Users => Set<User>();
    public DbSet<TelegramLink> TelegramLinks => Set<TelegramLink>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<IngredientPriceHistory> IngredientPriceHistories => Set<IngredientPriceHistory>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTimeOffset.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}