using Nastart.Domain.Entities;

namespace Nastart.Domain.Common;

public sealed class RecipeItem : BaseEntity
{
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public Guid IngredientId { get; set;}
    public Ingredient Ingredient { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal YieldPercentage { get; set; } = 1.0m; 
}