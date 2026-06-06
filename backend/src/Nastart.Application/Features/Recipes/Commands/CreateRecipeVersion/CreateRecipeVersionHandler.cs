using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;
using Nastart.Domain.Common;
using Nastart.Domain.Entities;

namespace Nastart.Application.Features.Recipes.Commands.CreateRecipeVersion;

public sealed class CreateRecipeVersionHandler(IAppDbContext db, ICostCascadeService cascade)
    : IRequestHandler<CreateRecipeVersionCommand, ErrorOr<CreateRecipeVersionResponse>>
{
    public async Task<ErrorOr<CreateRecipeVersionResponse>> Handle(
        CreateRecipeVersionCommand command, CancellationToken ct)
    {
        var sourceRecipe = await db.Recipes
            .AsNoTracking()
            .Include(r => r.RecipeItems)
            .FirstOrDefaultAsync(r => r.Id == command.SourceRecipeId && r.UserId == command.UserId, ct)
            .ConfigureAwait(false);

        if (sourceRecipe is null)
            return Error.NotFound("Recipe.NotFound", "Source recipe not found or doesn't belong to this user.");

        var newRecipe = new Recipe
        {
            Id = Guid.NewGuid(),
            UserId = sourceRecipe.UserId,
            Name = sourceRecipe.Name,
            PortionCount = sourceRecipe.PortionCount,
            TargetMargin = sourceRecipe.TargetMargin,
            CostPerPortion = 0m,
            VersionGroupId = sourceRecipe.VersionGroupId,
            VersionNumber = sourceRecipe.VersionNumber + 1,
            VersionLabel = command.VersionLabel
        };

        db.Recipes.Add(newRecipe);

        var copiedItems = sourceRecipe.RecipeItems.Select(
        ri => new RecipeItem
        {
            Id = Guid.NewGuid(),
            RecipeId = newRecipe.Id,
            IngredientId = ri.IngredientId,
            Quantity = ri.Quantity,
            YieldPercentage = ri.YieldPercentage
        }).ToList();

        foreach (var item in copiedItems)
            db.RecipeItems.Add(item);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var ingredientIds = sourceRecipe.RecipeItems
            .Select(ri => ri.IngredientId)
            .Distinct()
            .ToList();

        foreach (var ingredientId in ingredientIds)
        {
            await cascade.RecalculateForIngredientAsync(ingredientId, ct).ConfigureAwait(false);
        }

        var updatedNewRecipe = await db.Recipes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == newRecipe.Id, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("New recipe not found after creation.");

        return new CreateRecipeVersionResponse(
            updatedNewRecipe.Id,
            updatedNewRecipe.VersionGroupId,
            updatedNewRecipe.VersionNumber,
            updatedNewRecipe.VersionLabel
        );
    }
}