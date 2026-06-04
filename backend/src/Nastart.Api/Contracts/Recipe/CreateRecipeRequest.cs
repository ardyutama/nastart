namespace Nastart.Api.Contracts.Recipe;

public record CreateRecipeRequest(
    IReadOnlyList<CreateRecipeItemRequest> RecipeItems,
    string Name,
    int PortionCount,
    decimal PackagingCost = 0m,
    decimal TargetMargin = 0m,
    string VesionLabel = "Standart"
);

public record CreateRecipeItemRequest(Guid IngredientId, decimal Quantity, decimal YieldPercentage);

public record AddRecipeItemRequest(Guid IngredientId, decimal Quantity, decimal YieldPercentage);