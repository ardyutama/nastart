using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Ingredients.Commands.CreateIngredient;

public record CreateIngredientCommand(
    string Name,
    Guid UserId,
    Guid? CategoryId,
    Guid UnitId,
    decimal UnitSize,
    decimal PriceSpikeThresholdPct = 10m,
    decimal? InitialPrice = null,
    DateOnly? EffectiveDate = null
) : IRequest<ErrorOr<CreateIngredientResponse>>;