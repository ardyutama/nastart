using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;
using Nastart.Domain.Common;
using Nastart.Domain.Entities;

namespace Nastart.Application.Features.Recipes.Commands.CreateRecipe;

public sealed class CreateRecipeHandler(IAppDbContext db, ICostCascadeService cascade)
    : IRequestHandler<CreateRecipeCommand, ErrorOr<CreateRecipeResponse>>
{
    public async Task<ErrorOr<CreateRecipeResponse>> Handle(
        CreateRecipeCommand command, CancellationToken ct)
    {
        var isDuplicate = await db.Recipes
            .AnyAsync(r => r.UserId == command.UserId && r.Name == command.Name, ct)
            .ConfigureAwait(false);

        if(isDuplicate)
            return Error.Conflict("Recipe.Duplicate",
                "A recipe with this name already exists.");

        var ingredientIds = command.RecipeItems.Select(r => r.IngredientId).Distinct().ToList();
        var existingIngredientIds = await db.Ingredients
            .Where(i => i.UserId == command.UserId && ingredientIds.Contains(i.Id))
            .Select(i => i.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var versionGroupId = Guid.NewGuid();

        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            UserId = command.UserId,
            Name = command.Name,
            PortionCount = command.PortionCount,
            PackagingCost = command.PackagingCost,
            TargetMargin = command.TargetMargin,
            CostPerPortion = 0m,
            VersionGroupId = versionGroupId,
            VersionNumber = 1,
            VersionLabel = command.VersionLabel
        };

        db.Recipes.Add(recipe);

        var recipeItems = command.RecipeItems.Select(r => new RecipeItem
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            IngredientId = r.IngredientId,
            Quantity = r.Quantity,
            YieldPercentage = r.YieldQuantity
        }).ToList();

        foreach (var item in recipeItems)
            db.RecipeItems.Add(item);
        
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        foreach (var IngredientId in ingredientIds)
        {
            await cascade.RecalculateForIngredientAsync(IngredientId, ct)
                .ConfigureAwait(false);
        }

        var updatedRecipe = await db.Recipes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == recipe.Id, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Recipe not found after creation.");

        return new CreateRecipeResponse(
            updatedRecipe.Id,
            updatedRecipe.Name,
            updatedRecipe.PortionCount,
            updatedRecipe.CostPerPortion,
            updatedRecipe.VersionGroupId,
            updatedRecipe.VersionNumber
        );
    } 
}