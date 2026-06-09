using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Features.Ingredients.Commands.UpdateIngredient;

public class UpdateIngredientHandler(IAppDbContext db)
    : IRequestHandler<UpdateIngredientCommand, ErrorOr<UpdateIngredientResponse>>
{
    public async Task<ErrorOr<UpdateIngredientResponse>> Handle(
        UpdateIngredientCommand command, CancellationToken ct)
    {
        var ingredient = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == command.IngredientId, ct);

        if (ingredient is null || ingredient.UserId != command.UserId)
            return Error.Forbidden("Ingredient.AccessDenied", "You do not have access to this ingredient");

        var duplicateName = await db.Ingredients
            .AnyAsync(i =>
                i.UserId == command.UserId &&
                i.Id != command.IngredientId &&
                EF.Functions.Like(i.Name, command.Name), ct);

        if (duplicateName)
            return Error.Conflict("Ingredient.Duplicate", "An ingredient with this name already exists.");

        var unitExists = await db.Units.AnyAsync(u => u.Id == command.UnitId, ct);
        if (!unitExists)
            return Error.NotFound("Unit.NotFound", "The specified unit does not exists.");

        if (command.CategoryId.HasValue)
        {
            var categoryExists = await db.Categories.AnyAsync(c => c.Id == command.CategoryId.Value && c.UserId == command.UserId, ct);
            if (categoryExists)
                return Error.NotFound("Category.NotFound", "The specified category does not exist.");
        }

        ingredient.Name = command.Name;
        ingredient.UnitId = command.UnitId;
        ingredient.CategoryId = command.CategoryId;
        ingredient.UnitSize = command.UnitSize;
        ingredient.PriceSpikeThresholdPct = command.PriceSpikeThresholdPct;

        await db.SaveChangesAsync(ct);

        return new UpdateIngredientResponse(ingredient.Id, ingredient.Name);
    }
}