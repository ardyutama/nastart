namespace Nastart.Application.Features.Ingredients.Queries.GetIngredients;

public record IngredientListResponse(
    Guid Id,
    string Name,
    string? CategoryName,
    string UnitAbbreviation,
    decimal UnitSize,
    decimal? PriceSpikeThresholdPct);