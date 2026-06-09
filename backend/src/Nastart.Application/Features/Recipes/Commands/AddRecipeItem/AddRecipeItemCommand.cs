using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Recipes.Commands.AddRecipeItem;

public record AddRecipeItemCommand(
    Guid RecipeId,
    Guid UserId,
    Guid IngredientId,
    decimal Quantity,
    decimal YieldPercentage
) : IRequest<ErrorOr<AddRecipeItemResponse>>;