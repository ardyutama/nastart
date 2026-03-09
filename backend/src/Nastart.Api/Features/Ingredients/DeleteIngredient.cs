using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Ingredients;

public record DeleteIngredientCommand(Guid Id) : IRequest<bool>;

public class DeleteIngredientHandler(NastartDbContext db): IRequestHandler<DeleteIngredientCommand, bool>
{
    public async Task<bool> Handle(
        DeleteIngredientCommand request,
        CancellationToken cancellationToken)
    {
        var ingredient = await db.Ingredients.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (ingredient is null) return false;

        db.Ingredients.Remove(ingredient);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}