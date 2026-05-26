using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Recipes.Commands.CreateRecipe;

public record CreateRecipeCommand(
    Guid UserId,
    string Name,
    int PortionCount,
    IReadOnlyList<CreateRecipeItemDto> RecipeItems,
    decimal PackagingCost = 0m,
    decimal TargetMargin = 0m,
    string VersionLabel = "Standart"
) : IRequest<ErrorOr<CreateRecipeResponse>>;

public record CreateRecipeItemDto(Guid IngredientId, decimal Quantity, decimal YieldQuantity);