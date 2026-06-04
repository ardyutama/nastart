using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Features.Recipes.Commands.RemoveRecipeItem;

public sealed class RemoveRecipeItemHandler(IAppDbContext db, ICostCascadeService cascade)
    : IRequestHandler<RemoveRecipeItemCommand, ErrorOr<RemoveRecipeItemResponse>>
{
    public async Task<ErrorOr<RemoveRecipeItemResponse>> Handle(
        RemoveRecipeItemCommand command, CancellationToken ct)
    {
        var recipe = await db.Recipes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == command.RecipeId && r.UserId == command.UserId, ct)
            .ConfigureAwait(false);
        
        if(recipe is null)
            return Error.NotFound("Recipe.NotFound", "Recipe not found or doesn't belong to this user.");
        
        var recipeItem = await db.RecipeItems
            .FirstOrDefaultAsync(ri => ri.Id == command.RecipeItemId && ri.RecipeId == command.RecipeId, ct)
            .ConfigureAwait(false);
        
        if(recipeItem is null)
            return Error.NotFound("RecipeItem.NotFound", "Recipe item not found in this recipe.");
        
        var ingredientId = recipeItem.IngredientId;

        db.RecipeItems.Remove(recipeItem);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await cascade.RecalculateForIngredientAsync(ingredientId, ct).ConfigureAwait(false);

        var updatedRecipe = await db.Recipes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == command.RecipeId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Recipe not found after item removal.");
        
        return new RemoveRecipeItemResponse(
            command.RecipeId, updatedRecipe.CostPerPortion
        );
    }
}