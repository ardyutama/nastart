namespace Nastart.Application.Features.Recipes.Queries.GetRecipeById;

public record RecipeItemDetail(
    Guid RecipeItemId,
    Guid IngredientId,
    string Name,
    decimal Quantity,
    decimal YieldPercentage,
    decimal? CurrentPrice
);