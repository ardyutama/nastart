using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Recipes;

public record GetRecipeCostQuery(Guid RecipeId, Guid UserId) : IRequest<RecipeCostResponse?>;
public sealed record IngredientCostLine(
    Guid IngredientId,
    string IngredientName,
    string Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineCost);

public sealed record RecipeCostResponse(
    Guid RecipeId,
    string RecipeName,
    decimal TotalCost,
    decimal SellPrice,
    decimal MarginPercent,
    int YieldQuantity,
    string YieldUnit,
    decimal CostPerUnit,
    bool CostOutdated,
    List<IngredientCostLine> Lines);

public class GetRecipeCostHandler(NastartDbContext db) : IRequestHandler<GetRecipeCostQuery, RecipeCostResponse?>
{
    public async Task<RecipeCostResponse?> Handle(
        GetRecipeCostQuery request,
        CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes
            .AsNoTracking()
            .Where(r => r.Id == request.RecipeId && r.UserId == request.UserId)
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(cancellationToken);

        if (recipe is null) return null;

        var lines = recipe.RecipeIngredients
            .Select(ri => new IngredientCostLine(
                ri.IngredientId,
                ri.Ingredient.Name,
                ri.Ingredient.Unit,
                ri.Quantity,
                ri.Ingredient.CurrentPrice,
                ri.Quantity * ri.Ingredient.CurrentPrice
            )).ToList();

        var liveTotalCost = lines.Sum(l => l.LineCost);
        var costPerUnit = recipe.YieldQuantity > 0 ? liveTotalCost / recipe.YieldQuantity : liveTotalCost;

        var costOutdated = Math.Abs(liveTotalCost - recipe.TotalCost) > 0.01m;

        return new RecipeCostResponse(
            recipe.Id,
            recipe.Name,
            liveTotalCost,
            recipe.SellPrice,
            recipe.SellPrice > 0
                ? (recipe.SellPrice - liveTotalCost) / recipe.SellPrice * 100 : 0,
            recipe.YieldQuantity,
            recipe.YieldUnit,
            costPerUnit,
            costOutdated,
            lines
        );
    }
}