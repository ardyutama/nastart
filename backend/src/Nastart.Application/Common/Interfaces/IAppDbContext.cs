using Microsoft.EntityFrameworkCore;
using Nastart.Domain.Common;
using Nastart.Domain.Entities;

namespace Nastart.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<TelegramLink> TelegramLinks { get; }
    DbSet<Ingredient> Ingredients { get; }
    DbSet<IngredientPriceHistory> IngredientPriceHistories { get; }
    DbSet<Unit> Units { get; }
    DbSet<Category> Categories { get; }
    DbSet<Recipe> Recipes { get; }
    DbSet<RecipeItem> RecipeItems { get; }
    DbSet<CascadeErrorLog> CascadeErrorLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}