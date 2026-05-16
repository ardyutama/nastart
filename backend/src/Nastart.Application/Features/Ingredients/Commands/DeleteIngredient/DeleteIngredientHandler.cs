using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Features.Ingredients.Commands.DeleteIngredient;

public class DeleteIngredientHandlet(IAppDbContext db) 
    : IRequestHandler<DeleteIngredientCommand, ErrorOr<DeleteIngredientResponse>>
{
    public async Task<ErrorOr<DeleteIngredientResponse>> Handle(
        DeleteIngredientCommand command, CancellationToken ct)
    {
        var ingredient = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == command.IngredientId, ct);

        if (ingredient is null || ingredient.UserId != command.UserId)
            return Error.Forbidden("Ingredient.AccessDenied", "You do not access to this ingredient.");

        db.Ingredients.Remove(ingredient);
        await db.SaveChangesAsync(ct);

        return new DeleteIngredientResponse(ingredient.Id);
    }
}