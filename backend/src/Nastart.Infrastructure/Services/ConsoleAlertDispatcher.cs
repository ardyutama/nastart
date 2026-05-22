using Microsoft.Extensions.Logging;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Infrastructure.Services;

public sealed class ConsoleAlertDispatcher(
    ILogger<ConsoleAlertDispatcher> logger) : IAlertDispatcher
{
    public Task SendPriceSpikeAlertAsync(
        Guid userId,
        Guid ingredientId,
        decimal oldPrice,
        decimal newPrice,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "[ALERT STUB - Phase 2] Price spike on ingredient {IngredientId} for user {UserId}: "
            + "{OldPrice:C} -> {NewPrice:C}. "
            + "In Phase 4 (L15), this dispathes to Python FastApi -> Telegram. ",
            ingredientId,
            userId,
            oldPrice,
            newPrice
        );
    
        return Task.CompletedTask;
    }
}