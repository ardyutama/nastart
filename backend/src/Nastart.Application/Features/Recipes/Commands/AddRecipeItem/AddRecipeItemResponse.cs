namespace Nastart.Application.Features.Recipes.Commands.AddRecipeItem;

public record AddRecipeItemResponse(
    Guid RecipeItemId,
    Guid RecipeId,
    Guid IngredientId,
    decimal NewCostPerPortion
);