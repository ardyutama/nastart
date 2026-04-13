using Nastart.Api.Features.Ingredients;
using Nastart.Api.Features.Users;

namespace Nastart.Api.Features.Recipes;

public enum RecipeStatus
{
    Draft,
    Active,
    Inactive,
    Archived
}
public class Recipe
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required String Name { get; set; }
    public Decimal SellPrice { get; set; }
    public Decimal TotalCost { get; set; }
    public Decimal MarginPercent { get; set; }
    public Int32 YieldQuantity { get; set; }
    public required String YieldUnit { get; set; }
    public RecipeStatus Status { get; set; }
    public DateTimeOffset Created_at { get; set; }
    public User? User { get; set; }
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = [];
}

public class RecipeIngredient
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid IngredientId { get; set; }
    public Decimal Quantity { get; set; }
    public Decimal Cost { get; set; }
    public Recipe? Recipe { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
}