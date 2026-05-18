namespace Nastart.Application.Features.Ingredients.Commands.AddIngredientPrice;

public record AddIngredientPriceResponse(
    Guid IngredientId,
    decimal Price,
    DateOnly EffectiveDate,
    DateTimeOffset CommittedAt
);