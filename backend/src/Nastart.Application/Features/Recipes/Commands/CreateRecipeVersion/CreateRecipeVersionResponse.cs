namespace Nastart.Application.Features.Recipes.Commands.CreateRecipeVersion;

public record CreateRecipeVersionResponse(
    Guid NewRecipeId,
    Guid VersionGroupId,
    int VersionNumber,
    string VersionLabel
);