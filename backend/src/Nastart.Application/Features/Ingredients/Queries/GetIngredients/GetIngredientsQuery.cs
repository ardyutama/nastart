using MediatR;

namespace Nastart.Application.Features.Ingredients.Queries.GetIngredients;

public record GetIngredientsQuery(Guid UserId) : IRequest<GetIngredientsResponse>;