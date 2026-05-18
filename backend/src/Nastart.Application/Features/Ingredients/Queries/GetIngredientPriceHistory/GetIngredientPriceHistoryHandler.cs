using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Features.Ingredients.Queries.GetIngredientPriceHistory;

public class GetIngredientPriceHistoryHandler(IAppDbContext db)
    : IRequestHandler<GetIngredientPriceHistoryQuery, ErrorOr<GetIngredientPriceHistoryResponse>>
{
    public async Task<ErrorOr<GetIngredientPriceHistoryResponse>> Handle(
    GetIngredientPriceHistoryQuery query, CancellationToken ct)
    {
        var ingredient = await db.Ingredients
        .AsNoTracking()
        .FirstOrDefaultAsync(i => i.Id == query.IngredientId, ct);

        if (ingredient is null || ingredient.UserId != query.UserId)
            return Error.Forbidden("Ingredient.AccessDenied", "You do not have access to this ingredient.");

        var priceHistory = await db.IngredientPriceHistories
            .AsNoTracking()
            .Where(p => p.IngredientId == query.IngredientId)
            .OrderByDescending(p => p.CommittedAt)
            .Select(p => new PriceHistoryEntry(
                p.Id,
                p.Price,
                p.Source.ToString(),
                p.CommittedAt,
                p.EffectiveDate
            ))
            .ToListAsync(ct);

        return new GetIngredientPriceHistoryResponse(
            ingredient.Id,
            ingredient.Name,
            priceHistory
        );
    }

}