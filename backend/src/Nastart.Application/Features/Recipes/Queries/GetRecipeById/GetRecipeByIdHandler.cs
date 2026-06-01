using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Features.Recipes.Queries.GetRecipeById;

public sealed class GetRecipeByIdHandler(IAppDbContext db)
    : IRequestHandler<GetRecipeByIdQuery, ErrorOr<RecipeDetailResponse>>
{
    public async Task<ErrorOr<RecipeDetailResponse>> Handle(
        GetRecipeByIdQuery query, CancellationToken ct)
    {
        var recipe = await db.Recipes
            .AsNoTracking()
            .Include(r => r.RecipeItems)
            .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == query.RecipeId && r.UserId == query.UserId, ct)
            .ConfigureAwait(false);
        
        if(recipe is null)
            return Error.NotFound("Recipe.NotFound", "Recipe not found or doesn't belong to this user.");
        
        var ingredientIds = recipe.RecipeItems.Select(ri => ri.IngredientId).Distinct().ToList();
        var latestPrices = await db.IngredientPriceHistories
            .Where(ph => ingredientIds.Contains(ph.IngredientId))
            .GroupBy(ph => ph.IngredientId)
            .Select(g => new
            {
                IngredientId = g.Key,
                Price = (decimal?)g.OrderByDescending(ph => ph.CommittedAt).First().Price
            }).ToDictionaryAsync(x => x.IngredientId, x => x.Price, ct)
            .ConfigureAwait(false);

        var itemDetails = recipe.RecipeItems.Select(recipeItem =>
        {
            latestPrices.TryGetValue(recipeItem.IngredientId, out var currentPrice);
            return new RecipeItemDetail(
                recipeItem.Id,
                recipeItem.IngredientId,
                recipeItem.Ingredient.Name,
                recipeItem.Quantity,
                recipeItem.YieldPercentage,
                currentPrice
            );
        }).ToArray();

        decimal? derivedSellPrice = recipe.TargetMargin < 1m 
            ? (recipe.CostPerPortion + recipe.PackagingCost) / (1m - recipe.TargetMargin) : null;
        
        decimal? foodCostPct = derivedSellPrice.HasValue && derivedSellPrice.Value > 0
            ? (recipe.CostPerPortion / derivedSellPrice.Value) * 100m : null;
        
        return new RecipeDetailResponse(
            recipe.Id, recipe.Name, recipe.PortionCount,
            recipe.CostPerPortion, recipe.PackagingCost,
            recipe.TargetMargin,
            derivedSellPrice, foodCostPct,
            itemDetails,
            recipe.VersionNumber, recipe.VersionLabel,
            recipe.VersionGroupId
        );
    }
}