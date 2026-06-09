using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Features.Recipes.Queries.GetRecipes;

public sealed class GetRecipesHandler(IAppDbContext db)
    : IRequestHandler<GetRecipesQuery, ErrorOr<IReadOnlyList<RecipeResponse>>>
{
    public async Task<ErrorOr<IReadOnlyList<RecipeResponse>>> Handle(
        GetRecipesQuery query, CancellationToken ct)
    {
        var recipes = await db.Recipes
            .AsNoTracking()
            .Where(r => r.UserId == query.UserId)
            .OrderBy(r => r.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var response = recipes.Select(recipe =>
        {
            decimal? derivedSellPrice = recipe.TargetMargin < 1m
                ? (recipe.CostPerPortion + recipe.PackagingCost) / (1m - recipe.TargetMargin) : null;
            decimal? foodCostPct = derivedSellPrice.HasValue && derivedSellPrice.Value > 0
                ? (recipe.CostPerPortion / derivedSellPrice.Value) * 100m : null;
            return new RecipeResponse(
                recipe.Id, recipe.Name, recipe.PortionCount,
                recipe.CostPerPortion, recipe.PackagingCost, recipe.TargetMargin,
                derivedSellPrice, foodCostPct, recipe.VersionNumber,
                recipe.VersionLabel, recipe.VersionGroupId
            );
        }).ToArray();

        return response;
    }
}