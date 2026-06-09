namespace Nastart.Application.Features.Ingredients.Commands.AddIngredientPrice;

public record AddIngredientPriceResponse(
    Guid PriceHistoryId,
    decimal Price,
    int AffectedRecipe,
    int FailedRecipe
);