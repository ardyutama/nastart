namespace Nastart.Application.Features.Ingredients.Queries.GetIngredientPriceHistory;

public record PriceHistoryEntry(
    Guid Id,
    decimal Price,
    string Source,
    DateTimeOffset CommittedAt,
    DateOnly EffectiveDate
);

public record GetIngredientPriceHistoryResponse(
    Guid IngredientId,
    string IngredientName,
    List<PriceHistoryEntry> PriceHistory
);