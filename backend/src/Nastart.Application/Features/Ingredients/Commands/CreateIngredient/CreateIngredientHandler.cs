using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;
using Nastart.Domain.Entities;
using Nastart.Domain.Enums;

namespace Nastart.Application.Features.Ingredients.Commands.CreateIngredient;

public class CreateIngredientHandler(IAppDbContext db) : IRequestHandler<CreateIngredientCommand, ErrorOr<CreateIngredientResponse>>
{
    public async Task<ErrorOr<CreateIngredientResponse>> Handle(
        CreateIngredientCommand command, CancellationToken ct)
    {
        var unitExists = await db.Units
            .AnyAsync(u => u.Id == command.UnitId, ct)
            .ConfigureAwait(false);
        if (!unitExists)
            return Error.NotFound("Unit.NotFound", "The specified unit does not exists.");


        if (command.CategoryId.HasValue)
        {
            var categoryExists = await db.Categories
                .AnyAsync(c => c.Id == command.CategoryId.Value && c.UserId == command.UserId, ct);
            if (!categoryExists)
                return Error.NotFound("Category.NotFound", "The specified category does not found");
        }

        var exists = await db.Ingredients
            .AnyAsync(i => i.UserId == command.UserId && i.Name == command.Name, ct)
            .ConfigureAwait(false);

        if (exists)
            return Error.Conflict("Ingredient.Duplicate", "An ingredient with this name already exists. ");

        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            Name = command.Name,
            UserId = command.UserId,
            CategoryId = command.CategoryId,
            UnitId = command.UnitId,
            UnitSize = command.UnitSize,
            PriceSpikeThresholdPct = command.PriceSpikeThresholdPct
        };

        db.Ingredients.Add(ingredient);

        if (command.InitialPrice.HasValue)
        {
            var priceRecord = new IngredientPriceHistory
            {
                Id = Guid.NewGuid(),
                IngredientId = ingredient.Id,
                Price = command.InitialPrice.Value,
                UnitSize = command.UnitSize,
                Source = PriceSource.Manual,
                EffectiveDate = command.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow)
            };
            db.IngredientPriceHistories.Add(priceRecord);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new CreateIngredientResponse(ingredient.Id, ingredient.Name);
    }
}