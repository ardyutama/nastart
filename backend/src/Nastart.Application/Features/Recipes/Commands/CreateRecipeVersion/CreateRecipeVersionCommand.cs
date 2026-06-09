using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Recipes.Commands.CreateRecipeVersion;

public record CreateRecipeVersionCommand(
    Guid SourceRecipeId,
    Guid UserId,
    string VersionLabel
) : IRequest<ErrorOr<CreateRecipeVersionResponse>>;