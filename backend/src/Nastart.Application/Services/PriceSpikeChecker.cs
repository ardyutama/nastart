using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Services;

public sealed class PriceSpikeChecker(
    IAppDbContext db,
    IAlertDispatcher alertDispatcher,
    ILogger<PriceSpikeChecker> logger) : IPriceSpikeChecker
{
    public async Task CheckAndDispatchAsync(
        Guid ingredientId,
        Guid userId,
        decimal newPrice,
        CancellationToken cancellationToken)
    {
        var thresholdPct = await db.Ingredients
            .AsNoTracking()
            .Where(i => i.Id == ingredientId && i.UserId == userId)
            .Select(i => i.PriceSpikeThresholdPct)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (thresholdPct is null or <= 0m)
            return;

        var previousPrice = await db.IngredientPriceHistories
            .Where(iph => iph.IngredientId == ingredientId)
            .OrderByDescending(iph => iph.CommittedAt)
            .Skip(1)
            .Select(iph => (decimal?)iph.Price)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (previousPrice is null)
        {
            logger.LogInformation("Price spike check skipped for Ingredient {IngredientId}: first price entry", ingredientId);
            return;
        }

        decimal oldPrice = previousPrice.Value;
        decimal changePct = ((newPrice - oldPrice) / oldPrice) * 100m;
        decimal absChangePct = Math.Abs(changePct);

        if (absChangePct <= thresholdPct.Value)
            return;

        logger.LogWarning(
            "Price spike detected on ingredient {IngredientId}: {OldPrice} -> {NewPrice} "
            + "({ChangePct:F1}% change, threshold = {Threshold}%)",
            ingredientId,
            oldPrice,
            newPrice,
            changePct,
            thresholdPct.Value
        );

        try
        {
            await alertDispatcher.SendPriceSpikeAlertAsync(
                userId,
                ingredientId,
                oldPrice,
                newPrice,
                cancellationToken)
            .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to dispatch price spike alert for ingredient {IngredientId}",
                ingredientId
            );
        }
    }
}