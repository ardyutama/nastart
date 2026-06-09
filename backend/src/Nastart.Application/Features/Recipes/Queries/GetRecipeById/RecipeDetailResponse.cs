namespace Nastart.Application.Features.Recipes.Queries.GetRecipeById;

public record RecipeDetailResponse(
    Guid Id,
    string Name,
    int PortionCount,
    decimal CostPerPortion,
    decimal PackagingCost,
    decimal TargetMargin,
    decimal? DerivedSellPrice,
    decimal? FoodCostPct,
    IReadOnlyList<RecipeItemDetail> Items,
    int VersionNumber,
    string VersionLabel,
    Guid VersionGroupId
);