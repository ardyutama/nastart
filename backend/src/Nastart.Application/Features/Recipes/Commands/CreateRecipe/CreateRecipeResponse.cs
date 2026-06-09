namespace Nastart.Application.Features.Recipes.Commands.CreateRecipe;

public record CreateRecipeResponse(
    Guid Id,
    string Name,
    int PortionCount,
    decimal CostPerPortion,
    Guid VersionGroupId,
    int VersionNumber
);