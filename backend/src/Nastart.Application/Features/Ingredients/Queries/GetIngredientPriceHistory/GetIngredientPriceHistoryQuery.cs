using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Ingredients.Queries.GetIngredientPriceHistory;

public record GetIngredientPriceHistoryQuery(Guid IngredientId, Guid UserId)
    : IRequest<ErrorOr<GetIngredientPriceHistoryResponse>>;