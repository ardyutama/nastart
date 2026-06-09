namespace Nastart.Application.Features.Ingredients.Queries.GetIngredientById;

public record UnitDto(Guid Id, string Name, string Abbreviation);
public record CategoryDto(Guid Id, string Name);

public record GetIngredientByIdResponse(
    Guid Id,
    string Name,
    UnitDto Unit,
    CategoryDto? Category,
    decimal UnitSize,
    decimal? PriceSpikeThresholdPct,
    decimal? CurrentPrice,
    DateTimeOffset? LastPriceDate
);