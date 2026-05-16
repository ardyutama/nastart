using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Ingredients.Commands.UpdateIngredient;

public record UpdateIngredientCommand(
    Guid IngredientId,
    Guid UserId,
    string Name,
    Guid UnitId,
    Guid? CategoryId,
    decimal UnitSize,
    decimal? PriceSpikeThresholdPct = null
) : IRequest<ErrorOr<UpdateIngredientResponse>>;