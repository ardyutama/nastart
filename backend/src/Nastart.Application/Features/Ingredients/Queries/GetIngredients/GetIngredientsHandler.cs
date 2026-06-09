using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Features.Ingredients.Queries.GetIngredients;

public class GetIngredientsHandler(IAppDbContext db)
        : IRequestHandler<GetIngredientsQuery, GetIngredientsResponse>
{
    public async Task<GetIngredientsResponse> Handle(
        GetIngredientsQuery request, CancellationToken cancellationToken)
    {
        var ingredients = await db.Ingredients
            .AsNoTracking()
            .Where(i => i.UserId == request.UserId)
            .OrderBy(i => i.Name)
            .Select(i => new IngredientSummary(
                i.Id,
                i.Name,
                i.Unit.Abbreviation,
                i.Category != null ? i.Category.Name : "Uncategorized",
                i.UnitSize,
                i.PriceHistory
                    .OrderByDescending(p => p.CommittedAt)
                    .Select(p => (decimal?)p.Price)
                    .FirstOrDefault()
            ))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new GetIngredientsResponse(ingredients);
    }
}