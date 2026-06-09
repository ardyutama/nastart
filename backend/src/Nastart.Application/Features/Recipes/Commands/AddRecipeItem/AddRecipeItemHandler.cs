using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;
using Nastart.Domain.Common;

namespace Nastart.Application.Features.Recipes.Commands.AddRecipeItem;

public sealed class AddRecipeItemHandler(IAppDbContext db, ICostCascadeService cascade)
    : IRequestHandler<AddRecipeItemCommand, ErrorOr<AddRecipeItemResponse>>
{
    public async Task<ErrorOr<AddRecipeItemResponse>> Handle(
        AddRecipeItemCommand command, CancellationToken ct)
    {
        var recipe = await db.Recipes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == command.RecipeId && r.UserId == command.UserId, ct)
            .ConfigureAwait(false);

        if (recipe is null)
            return Error.NotFound("Recipe.NotFound", "Recipe not found or doesn't belong to this user.");

        var ingredient = await db.Ingredients
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == command.IngredientId && i.UserId == command.UserId, ct)
            .ConfigureAwait(false);

        if (ingredient is null)
            return Error.NotFound("Ingredient.NotFound", "Ingredient not found or doesn't belong to this user.");

        var alreadyExist = await db.RecipeItems
            .AnyAsync(ri => ri.RecipeId == command.RecipeId && ri.IngredientId == command.IngredientId, ct)
            .ConfigureAwait(false);

        if (alreadyExist)
            return Error.Conflict("RecipeItem.Duplicate", "This ingredient is already in the recipe. Use UpdateRecipeItem to change quantity.");

        var recipeItem = new RecipeItem
        {
            Id = Guid.NewGuid(),
            RecipeId = command.RecipeId,
            IngredientId = command.IngredientId,
            Quantity = command.Quantity,
            YieldPercentage = command.YieldPercentage
        };

        db.RecipeItems.Add(recipeItem);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await cascade.RecalculateForIngredientAsync(command.IngredientId, ct).ConfigureAwait(false);

        var updatedRecipe = await db.Recipes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == command.RecipeId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Recipe not found after item addition.");

        return new AddRecipeItemResponse(
            recipeItem.Id,
            recipeItem.RecipeId,
            recipeItem.IngredientId,
            updatedRecipe.CostPerPortion
        );
    }
}