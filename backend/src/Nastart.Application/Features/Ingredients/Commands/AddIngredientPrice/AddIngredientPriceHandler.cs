using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;
using Nastart.Domain.Entities;
using Nastart.Domain.Enums;

namespace Nastart.Application.Features.Ingredients.Commands.AddIngredientPrice;

public class AddIngredientPriceHandler(IAppDbContext db)
    : IRequestHandler<AddIngredientPriceCommand, ErrorOr<AddIngredientPriceResponse>>
{
    public async Task<ErrorOr<AddIngredientPriceResponse>> Handle(
        AddIngredientPriceCommand command, CancellationToken ct)
    {
        var ingredient = await db.Ingredients
            .FirstOrDefaultAsync(i => i.Id == command.IngredientId, ct);

        if (ingredient is null || ingredient.UserId != command.UserId)
            return Error.Forbidden("Ingredient.AccessDenied", "You do not have access to this ingredient.");

        var effectiveDate = command.EffectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var priceRecord = new IngredientPriceHistory
        {
            Id = Guid.NewGuid(),
            IngredientId = command.IngredientId,
            Price = command.Price,
            UnitSize = ingredient.UnitSize,
            Source = PriceSource.Manual,
            CommittedAt = DateTimeOffset.UtcNow,
            EffectiveDate = effectiveDate
        };

        db.IngredientPriceHistories.Add(priceRecord);
        await db.SaveChangesAsync(ct);

        return new AddIngredientPriceResponse(
            command.IngredientId,
            command.Price,
            effectiveDate,
            priceRecord.CommittedAt
        );
    }
}