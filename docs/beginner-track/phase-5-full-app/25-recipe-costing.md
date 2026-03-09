# Lesson 25: Recipe Costing — Calculating Cost and Margin from Current Prices

> **Phase**: 5 — Building the Full Application
> **Prerequisite**: Lesson 24. Recipes exist with `RecipeIngredient` lines.
> **What you build**: `GET /recipes/{id}/cost` calculates the total ingredient cost and profit margin, then writes the results back to `last_cost` and `margin_percent` on the recipe.

---

## What You Will Learn

- How to compute a derived value from related data using LINQ
- How to write back a computed result to the database (cost cache write-back)
- Why separating query from write-back is intentional here

---

## The Business Logic

To cost a recipe, you multiply each ingredient line's `quantity` by the ingredient's current `price_per_unit`, then sum all lines:

```
total_cost = Σ (quantity × price_per_unit)  for each recipe ingredient

margin_percent = (selling_price - total_cost) / selling_price × 100
```

You then store `total_cost` as `last_cost` and the computed margin as `margin_percent` on the recipe. This is a **write-back cache** — a pre-computed value stored in the DB so you don't recalculate it on every dashboard load.

---

## Step 1 — GetRecipeCostQuery

Create `src/Nastart.Api/Features/Recipes/GetRecipeCost.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Services;

namespace Nastart.Api.Features.Recipes;

// ── Query ──────────────────────────────────────────────────────────

public sealed record GetRecipeCostQuery(Guid RecipeId)
    : IRequest<RecipeCostResponse>;

// ── Response ───────────────────────────────────────────────────────

public sealed record CostLineResponse(
    Guid    IngredientId,
    string  IngredientName,
    string  Unit,
    decimal Quantity,
    decimal PricePerUnit,
    decimal LineCost);

public sealed record RecipeCostResponse(
    Guid                    RecipeId,
    string                  RecipeName,
    decimal                 TotalCost,
    decimal?                SellingPrice,
    decimal?                MarginPercent,
    List<CostLineResponse>  Lines);

// ── Handler ────────────────────────────────────────────────────────

public sealed class GetRecipeCostHandler(NastartDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetRecipeCostQuery, RecipeCostResponse>
{
    public async Task<RecipeCostResponse> Handle(
        GetRecipeCostQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Load the recipe with all ingredient lines and their current prices
        var recipe = await db.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(
                r => r.Id == request.RecipeId && r.UserId == currentUser.UserId,
                cancellationToken);

        if (recipe is null)
            throw new RecipeNotFoundException(request.RecipeId);

        // 2. Compute cost per line
        var lines = recipe.RecipeIngredients
            .Select(ri => new CostLineResponse(
                ri.IngredientId,
                ri.Ingredient!.Name,
                ri.Unit,
                ri.Quantity,
                ri.Ingredient.PricePerUnit,
                Math.Round(ri.Quantity * ri.Ingredient.PricePerUnit, 4)))
            .ToList();

        var totalCost = lines.Sum(l => l.LineCost);

        // 3. Compute margin if selling price is set
        decimal? marginPercent = recipe.SellingPrice.HasValue && recipe.SellingPrice > 0
            ? Math.Round(
                (recipe.SellingPrice.Value - totalCost) / recipe.SellingPrice.Value * 100, 4)
            : null;

        // 4. Write the results back (cache write-back)
        recipe.LastCost      = totalCost;
        recipe.MarginPercent = marginPercent;
        await db.SaveChangesAsync(cancellationToken);

        return new RecipeCostResponse(
            recipe.Id,
            recipe.Name,
            totalCost,
            recipe.SellingPrice,
            marginPercent,
            lines);
    }
}
```

---

## Step 2 — UpdateRecipeCost Command (Explicit Recalculate)

You may also want a `PATCH /recipes/{id}/recalculate-cost` endpoint that can be called programmatically — for example, after a price spike notification triggers a full re-cost of all active recipes.

Create `src/Nastart.Api/Features/Recipes/UpdateRecipeCost.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Services;

namespace Nastart.Api.Features.Recipes;

/// <summary>
/// Recalculates and persists the cost for one recipe.
/// Reuses the same logic as GetRecipeCostQuery but is a Command (explicit write intent).
/// </summary>
public sealed record UpdateRecipeCostCommand(Guid RecipeId) : IRequest<RecipeCostResponse>;

public sealed class UpdateRecipeCostHandler(NastartDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateRecipeCostCommand, RecipeCostResponse>
{
    public Task<RecipeCostResponse> Handle(
        UpdateRecipeCostCommand request,
        CancellationToken cancellationToken)
    {
        // Delegate to GetRecipeCostQuery which already handles the write-back
        var inner = new GetRecipeCostHandler(db, currentUser);
        return inner.Handle(new GetRecipeCostQuery(request.RecipeId), cancellationToken);
    }
}
```

> **Note**: Reusing `GetRecipeCostHandler` directly is an intentional pragmatic choice for now. In a larger codebase you would extract the shared calculation logic into a private helper method or a domain service.

---

## Step 3 — Add Routes

In `RecipesEndpoints.cs`, add:

```csharp
// GET /recipes/{id}/cost — calculate and write back
group.MapGet("/{id}/cost", async (
    Guid id,
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(new GetRecipeCostQuery(id), ct);
    return TypedResults.Ok(result);
})
.WithName("GetRecipeCost")
.WithSummary("Calculate recipe cost from current ingredient prices");

// PATCH /recipes/{id}/recalculate-cost
group.MapPatch("/{id}/recalculate-cost", async (
    Guid id,
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(new UpdateRecipeCostCommand(id), ct);
    return TypedResults.Ok(result);
})
.WithName("RecalculateRecipeCost")
.WithSummary("Explicitly recalculate and persist recipe cost");
```

---

## ✅ Test It

First ensure your recipe has ingredients with prices set in Lesson 22 and a `sellingPrice`:

```http
### Cost the recipe
GET http://localhost:5000/recipes/{{recipeId}}/cost
Authorization: Bearer {{token}}
```

Expected response:

```json
{
  "recipeId": "...",
  "recipeName": "Sourdough Loaf",
  "totalCost": 0.7210,
  "sellingPrice": 8.00,
  "marginPercent": 90.9875,
  "lines": [
    {
      "ingredientName": "Bread Flour",
      "quantity": 0.5,
      "unit": "kg",
      "pricePerUnit": 1.20,
      "lineCost": 0.6000
    },
    {
      "ingredientName": "Salt",
      "quantity": 0.01,
      "unit": "kg",
      "pricePerUnit": 1.21,
      "lineCost": 0.0121
    }
  ]
}
```

After calling this endpoint, verify the `last_cost` and `margin_percent` columns on the recipe row in the database are updated:

```sql
SELECT id, name, last_cost, margin_percent FROM recipes WHERE id = '...';
```

---

## What Actually Happens in the SQL

The `Include(...).ThenInclude(...)` call generates a single JOIN query:

```sql
SELECT r.*, ri.*, i.*
FROM   recipes r
JOIN   recipe_ingredients ri ON ri.recipe_id = r.id
JOIN   ingredients i ON i.id = ri.ingredient_id
WHERE  r.id = @recipeId
AND    r.user_id = @userId
```

EF Core then materializes this into the object graph you traverse in C# with `.RecipeIngredients[n].Ingredient.PricePerUnit`.

---

## Key Concepts

| Term | Definition |
|---|---|
| **Write-back cache** | Pre-computing and storing a derived value (`last_cost`) to avoid recalculating it on every read. |
| **`ThenInclude`** | EF Core syntax for loading a navigation property of a navigation property (two levels deep). |
| **`lines.Sum(l => l.LineCost)`** | LINQ `.Sum()` on an in-memory list — runs in C#, not in SQL. |
| **Cost vs. Margin** | Cost is what you pay; margin is what percentage of the selling price is profit. |
| **Command vs. Query write-back** | `GET /cost` also writes — a pragmatic choice. `PATCH /recalculate-cost` makes the write intent explicit for programmatic callers. |

---

## Next

Lesson 26 → **Record Purchase** — `POST /purchases` records buying ingredients, updates stock, and updates the ingredient's current price — all inside a single database transaction using `IDbContextTransaction`.
