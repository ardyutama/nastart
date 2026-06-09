using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Recipes.Queries.GetRecipes;

public record GetRecipesQuery(Guid UserId) : IRequest<ErrorOr<IReadOnlyList<RecipeResponse>>>;