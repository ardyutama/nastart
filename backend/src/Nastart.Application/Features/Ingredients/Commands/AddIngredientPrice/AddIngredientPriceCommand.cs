using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Ingredients.Commands.AddIngredientPrice;

public record AddIngredientPriceCommand(
    Guid IngredientId,
    Guid UserId,
    decimal Price,
    DateOnly? EffectiveDate = null
) : IRequest<ErrorOr<AddIngredientPriceResponse>>;