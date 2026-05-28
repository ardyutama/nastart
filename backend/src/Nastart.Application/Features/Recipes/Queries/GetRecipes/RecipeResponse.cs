namespace Nastart.Application.Features.Recipes.Queries.GetRecipes;

public record RecipeResponse(
    Guid Id,
    string Name,
    int PortionCount,
    decimal CostPerPortion,
    decimal PackagingCost,
    decimal TargetMargin,
    decimal? DerivedSellPrice,
    decimal? FoodCostPCt,
    int VersionNumber,
    string VersionLabel,
    Guid VersionGroupId
);