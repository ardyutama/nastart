namespace Nastart.Application.Features.Recipes.Commands.RemoveRecipeItem;

public record RemoveRecipeItemResponse(
    Guid RecipeId,
    decimal NewCostPerPortion
);