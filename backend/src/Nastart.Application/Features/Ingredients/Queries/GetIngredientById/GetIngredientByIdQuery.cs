using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Ingredients.Queries.GetIngredientById;

public record GetIngredientByIdQuery(Guid IngredientId, Guid UserId)
    : IRequest<ErrorOr<GetIngredientByIdResponse>>;