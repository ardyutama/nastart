using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Recipes.Queries.GetRecipeById;

public record GetRecipeByIdQuery(Guid RecipeId, Guid UserId)
    : IRequest<ErrorOr<RecipeDetailResponse>>;