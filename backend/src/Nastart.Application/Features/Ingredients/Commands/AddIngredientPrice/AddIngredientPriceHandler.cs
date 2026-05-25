using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nastart.Application.Common.Interfaces;
using Nastart.Domain.Entities;
using Nastart.Domain.Enums;

namespace Nastart.Application.Features.Ingredients.Commands.AddIngredientPrice;

public class AddIngredientPriceHandler(
        IAppDbContext db,
        ICostCascadeService cascadeService,
        IPriceSpikeChecker priceSpikeChecker,
        ILogger<AddIngredientPriceHandler> logger
    ): IRequestHandler<AddIngredientPriceCommand, ErrorOr<AddIngredientPriceResponse>>
{
    public async Task<ErrorOr<AddIngredientPriceResponse>> Handle(
        AddIngredientPriceCommand command, CancellationToken ct)
    {
        var ingredient = await db.Ingredients
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == command.IngredientId, ct)
            .ConfigureAwait(false);

        if (ingredient is null || ingredient.UserId != command.UserId)
            return Error.Forbidden("Ingredient.AccessDenied", "You do not have access to this ingredient.");

        var priceRecord = new IngredientPriceHistory
        {
            Id = Guid.NewGuid(),
            IngredientId = command.IngredientId,
            Price = command.Price,
            UnitSize = ingredient.UnitSize,
            Source = PriceSource.Manual,
            CommittedAt = DateTimeOffset.UtcNow,
            EffectiveDate = command.EffectiveDate ?? DateOnly.FromDateTime(DateTime.Now)
        };

        db.IngredientPriceHistories.Add(priceRecord);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "New price committed for ingredient {IngredientId}: {Price}",
            ingredient.Id,
            command.Price
        );

        await priceSpikeChecker.CheckAndDispatchAsync(
            ingredient.Id,
            ingredient.UserId,
            command.Price,
            ct
        ).ConfigureAwait(false);

        var cascadeResult = await cascadeService.RecalculateForIngredientAsync(
            ingredient.Id,
            ct
        ).ConfigureAwait(false);

        logger.LogInformation(
            "Cascade complete for ingredient {IngredientId}: {AffectedRecipes} recipes updated, {FailedRecipes} failed",
            ingredient.Id,
            cascadeResult.AffectedRecipes,
            cascadeResult.FailedRecipes
        );

        return new AddIngredientPriceResponse(
            command.IngredientId,
            command.Price,
            cascadeResult.AffectedRecipes,
            cascadeResult.FailedRecipes
        );
    }
}