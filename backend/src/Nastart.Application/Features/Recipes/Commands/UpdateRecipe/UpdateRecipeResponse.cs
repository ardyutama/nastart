namespace Nastart.Application.Features.Recipes.Commands.UpdateRecipe;

public record UpdateRecipeResponse(
    Guid Id,
    String Name,
    decimal CostPerPortion
);