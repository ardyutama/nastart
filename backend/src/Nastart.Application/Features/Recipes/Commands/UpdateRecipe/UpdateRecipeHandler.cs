using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Features.Recipes.Commands.UpdateRecipe;

public sealed class UpdateRecipeHandler(IAppDbContext db, ICostCascadeService cascade)
    : IRequestHandler<UpdateRecipeCommand, ErrorOr<UpdateRecipeResponse>>
{
    public async Task<ErrorOr<UpdateRecipeResponse>> Handle(
        UpdateRecipeCommand command, CancellationToken ct)
    {
        var recipe = await db.Recipes
            .Include(r => r.RecipeItems)
            .FirstOrDefaultAsync(r => r.Id == command.RecipeId && r.UserId == command.UserId, ct)
            .ConfigureAwait(false);
        
        if(recipe is null)
            return Error.NotFound("Recipe.NotFound", "Recipe not found or doesn't belong to this user.");
        
        if(recipe.Name != command.Name)
        {
            var isDuplicate = await db.Recipes
                .AnyAsync(r => r.UserId == command.UserId && EF.Functions.Like(r.Name, command.Name) && r.Id != command.RecipeId)
                .ConfigureAwait(false);

            if(isDuplicate)
                return Error.Conflict("Recipe.Duplicate", "A recipe with this name already exists.");
        }

        var portionCountChanged = recipe.PortionCount != command.PortionCount;

        recipe.Name = command.Name;
        recipe.PortionCount = command.PortionCount;
        recipe.PackagingCost = command.PackagingCost;
        recipe.TargetMargin = command.TargetMargin;
        recipe.VersionLabel = command.VersionLabel;

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        if(portionCountChanged)
        {
            var ingredientIds = recipe.RecipeItems
                .Select(ri => ri.IngredientId)
                .Distinct()
                .ToList();
            
            foreach(var IngredientId in ingredientIds)
            {
                await cascade.RecalculateForIngredientAsync(IngredientId, ct).ConfigureAwait(false);
            }

            recipe = await db.Recipes
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == command.RecipeId, ct)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("Recipe not found after update.");
        }
        
        return new UpdateRecipeResponse(
            recipe.Id,
            recipe.Name,
            recipe.CostPerPortion
        );
    }
}