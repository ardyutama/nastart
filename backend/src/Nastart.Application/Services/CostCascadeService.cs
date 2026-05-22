using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nastart.Application.Common.Interfaces;
using Nastart.Domain.Entities;

namespace Nastart.Application.Services;

public sealed class CostCascadeService(
    IAppDbContext db,
    ILogger<CostCascadeService> logger) : ICostCascadeService
{
    public async Task<CascadeResult> RecalculateForIngredientAsync(
        Guid ingredientId,
        CancellationToken cancellationToken)
    {
        var currentPriceRecord = await db.IngredientPriceHistories
            .AsNoTracking()
            .Where(iph => iph.IngredientId == ingredientId)
            .OrderByDescending(iph => iph.CommittedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if(currentPriceRecord is null)
        {
            logger.LogWarning(
                "Cascade skipped for ingredient {IngredientId}: no price history found",
                ingredientId);
            return new CascadeResult(0, 0);
        }

        var affectedRecipeIds = await db.RecipeItems
            .Where(ri => ri.IngredientId == ingredientId)
            .Select(ri => ri.RecipeId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if(affectedRecipeIds.Count == 0)
        {
            logger.LogInformation("No recipes use ingredient {IngredientId}", ingredientId);
            return new CascadeResult(0, 0);
        }

        int succesCount = 0;
        int failCount = 0;

        foreach(var recipeId in affectedRecipeIds)
        {
            try
            {
                await RecalculateForIngredientAsync(recipeId, cancellationToken).ConfigureAwait(false);
                succesCount++;
            }
            catch (Exception ex)
            {
                failCount++;
                logger.LogError(ex, "Cascade failed for recipe {RecipeId} (triggered by ingredient {IngredientId})",
                recipeId,
                ingredientId);

                db.CascadeErrorLogs.Add(new CascadeErrorLog
                {
                    Id = Guid.NewGuid(),
                    IngredientId = ingredientId,
                    RecipeId = recipeId,
                    ErrorMessage = ex.Message
                });

                try
                {
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception logEx)
                {
                    logger.LogError(
                        logEx,
                        "Failed to write CascadeErrorLog for recipe {RecipeId}",
                        recipeId);
                }
            }
        }
        return new CascadeResult(succesCount, failCount);
    }

    private async Task RecalculateRecipeAsync(Guid recipeId, CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes
            .Include(r => r.RecipeItems)
            .FirstOrDefaultAsync(r => r.Id == recipeId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Recipe {recipeId} not found during cascade"
            );

        var ingredientIds = recipe.RecipeItems
            .Select(ri => ri.IngredientId)
            .Distinct()
            .ToList();
        
        var latestPrices = await db.IngredientPriceHistories
            .Where(iph => ingredientIds.Contains(iph.IngredientId))
            .GroupBy(iph => iph.IngredientId)
            .Select(g => g.OrderByDescending(iph => iph.CommittedAt)
                .Select(iph => new { iph.IngredientId, iph.Price, iph.UnitSize}).First())
            .ToDictionaryAsync(x => x.IngredientId, x => x, cancellationToken)
            .ConfigureAwait(false);
        
        decimal totalCost = 0m;

        foreach (var item in recipe.RecipeItems)
        {
            if(!latestPrices.TryGetValue(item.IngredientId, out var latestPrice) || latestPrices is null)
            {
                throw new InvalidOperationException(
                    $"Ingredient {item.IngredientId} in recipe {recipeId} has no price history.");
            }

            if (latestPrice.UnitSize <= 0m)
                throw new InvalidOperationException($"Ingredient {item.IngredientId} has invalid unit size in latest price history.");

            var itemCost = (latestPrice.Price / latestPrice.UnitSize)
                * item.Quantity
                * (1m / item.YieldPercentage);
            
            totalCost += itemCost;
        }

        recipe.CostPerPortion = recipe.PortionCount > 0
            ? Math.Round(totalCost / recipe.PortionCount, 4) : 0m;
        
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Recipe {RecipeId} recalculated: cost_per_portion = {CostPerPortion}",
            recipeId,
            recipe.CostPerPortion);
    }
}