using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Features.Ingredients.Queries.GetIngredients;

public class GetIngredientsHandler(IAppDbContext db)
        : IRequestHandler<GetIngredientsQuery, List<IngredientListResponse>>
{
    private readonly IAppDbContext _db = db;

    public async Task<List<IngredientListResponse>> Handle(
        GetIngredientsQuery request, CancellationToken cancellationToken)
    {
        var ingredients = await _db.Ingredients
            .AsNoTracking()
            .Where(i => i.UserId == request.UserId)
            .Select(i => new IngredientListResponse(
                i.Id,
                i.Name,
                i.Category != null ? i.Category.Name : null,
                i.Unit.Abbrevation,
                i.UnitSize,
                i.PriceSpikeThresholdPct))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ingredients;
    }
}