namespace Nastart.Api.Features.Ingredients;

public record CreateIngredientRequest(
    string Name,
    string? Unit,
    decimal CurrentPrice,
    decimal CurrentStock,
    decimal MinStock
);

public record UpdateIngredientRequest(
    string? Name,
    string? Unit,
    decimal? CurrentPrice,
    decimal? CurrentStock,
    decimal? MinStock
);