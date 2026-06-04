using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Recipes.Commands.RemoveRecipeItem;

public record RemoveRecipeItemCommand(
    Guid RecipeItemId,
    Guid RecipeId,
    Guid UserId
) : IRequest<ErrorOr<RemoveRecipeItemResponse>>;