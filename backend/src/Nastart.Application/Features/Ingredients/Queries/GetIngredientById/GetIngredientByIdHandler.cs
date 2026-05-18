using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Features.Ingredients.Queries.GetIngredientById;

public class GetIngredientByIdHandler(IAppDbContext db)
    : IRequestHandler<GetIngredientByIdQuery, ErrorOr<GetIngredientByIdResponse>>
{
    public async Task<ErrorOr<GetIngredientByIdResponse>> Handle(
        GetIngredientByIdQuery query, CancellationToken ct)
    {
        var ingredient = await db.Ingredients
            .AsNoTracking()
            .Include(i => i.Unit)
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == query.IngredientId, ct);

        if (ingredient is null || ingredient.UserId != query.UserId)
            return Error.Forbidden("Ingredient.AccessDenied", "You do not have access to this ingredient.");

        var latestPrice = await db.IngredientPriceHistories
            .AsNoTracking()
            .Where(p => p.IngredientId == query.IngredientId)
            .OrderByDescending(p => p.CommittedAt)
            .FirstOrDefaultAsync(ct);

        return new GetIngredientByIdResponse(
            ingredient.Id,
            ingredient.Name,
            new UnitDto(ingredient.Unit.Id, ingredient.Unit.Name, ingredient.Unit.Abbreviation),
            ingredient.Category is not null ? new CategoryDto(ingredient.Category.Id, ingredient.Category.Name) : null,
            ingredient.UnitSize,
            ingredient.PriceSpikeThresholdPct,
            latestPrice?.Price,
            latestPrice?.CommittedAt
        );
    }
}