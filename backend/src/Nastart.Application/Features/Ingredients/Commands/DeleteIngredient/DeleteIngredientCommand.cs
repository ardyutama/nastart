using ErrorOr;
using MediatR;

namespace Nastart.Application.Features.Ingredients.Commands.DeleteIngredient;

public record DeleteIngredientCommand(Guid IngredientId, Guid UserId) : IRequest<ErrorOr<DeleteIngredientResponse>>;