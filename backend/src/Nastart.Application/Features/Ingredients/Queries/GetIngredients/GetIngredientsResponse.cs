namespace Nastart.Application.Features.Ingredients.Queries.GetIngredients;

public record IngredientSummary(
    Guid Id,
    string Name,
    string UnitAbbreviation,
    string CategoryName,
    decimal UnitSize,
    decimal? CurrentPrice
);

public record GetIngredientsResponse(List<IngredientSummary> Ingredients);