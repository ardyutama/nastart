using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Recipes.Commands.UpdateRecipe;

public record UpdateRecipeCommand(
    Guid RecipeId,
    Guid UserId,
    string Name,
    int PortionCount,
    decimal PackagingCost,
    decimal TargetMargin,
    string VersionLabel
) : IRequest<ErrorOr<UpdateRecipeResponse>>;