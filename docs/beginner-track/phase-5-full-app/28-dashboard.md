# Lesson 28: Dashboard — Parallel Queries with Task.WhenAll

> **Phase**: 5 — Building the Full Application
> **Prerequisite**: Lesson 27. The full schema is in place and all features are working.
> **What you build**: `GET /dashboard` returns a business overview — inventory, recipe count, recent spend — assembled from multiple parallel database queries.

---

## What You Will Learn

- How to run multiple independent database queries in parallel with `Task.WhenAll`
- How to compose a summary response from diverse data sources
- Why `Task.WhenAll` is faster than running queries sequentially for read-only dashboards

---

## Why Parallel Queries?

The dashboard needs data from four different tables. Running them sequentially wastes time:

```
Sequential:  ingredients query (50ms) → recipes query (30ms) → purchases query (40ms) = 120ms total
Parallel:    all three fire at once                                                    =  50ms total
```

`Task.WhenAll` starts all tasks simultaneously and waits until the last one finishes.

```python
# Python analogy (asyncio):
results = await asyncio.gather(
    get_ingredient_stats(),
    get_recipe_stats(),
    get_purchase_stats()
)
```

---

## The Dashboard Response

```
GET /dashboard → {
  inventory: {
    totalIngredients: 12,
    lowStockCount: 3,
    lowStockIngredients: [ { name, stockQuantity, reorderThreshold } ]
  },
  recipes: {
    totalRecipes: 8,
    activeRecipes: 5,
    avgMarginPercent: 62.3
  },
  purchases: {
    last30DaysTotal: 248.75,
    recentPurchases: [ { ingredientName, totalCost, purchasedAt } ]
  }
}
```

---

## Step 1 — Create GetDashboardSummaryQuery

Create `src/Nastart.Api/Features/Dashboard/GetDashboardSummary.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Recipes;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Services;

namespace Nastart.Api.Features.Dashboard;

// ── Query ──────────────────────────────────────────────────────────

public sealed record GetDashboardSummaryQuery : IRequest<DashboardResponse>;

// ── Response types ─────────────────────────────────────────────────

public sealed record LowStockItem(
    Guid     IngredientId,
    string   Name,
    decimal  StockQuantity,
    decimal  ReorderThreshold);

public sealed record InventorySummary(
    int              TotalIngredients,
    int              LowStockCount,
    List<LowStockItem> LowStockIngredients);

public sealed record RecipeSummary(
    int      TotalRecipes,
    int      ActiveRecipes,
    decimal? AvgMarginPercent);

public sealed record RecentPurchaseItem(
    string         IngredientName,
    decimal        TotalCost,
    DateTimeOffset PurchasedAt);

public sealed record PurchaseSummary(
    decimal                  Last30DaysTotal,
    List<RecentPurchaseItem> RecentPurchases);

public sealed record DashboardResponse(
    InventorySummary  Inventory,
    RecipeSummary     Recipes,
    PurchaseSummary   Purchases);

// ── Handler ────────────────────────────────────────────────────────

public sealed class GetDashboardSummaryHandler(NastartDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetDashboardSummaryQuery, DashboardResponse>
{
    public async Task<DashboardResponse> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30);

        // ── Fire all queries in parallel ───────────────────────────
        // Each query is independent — no query depends on another's result.
        // Task.WhenAll starts all four immediately and waits for all to finish.

        var ingredientsTask = db.Ingredients
            .AsNoTracking()
            .Where(i => i.UserId == userId)
            .Select(i => new
            {
                i.Id,
                i.Name,
                i.StockQuantity,
                i.ReorderThreshold
            })
            .ToListAsync(cancellationToken);

        var recipeSummaryTask = db.Recipes
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .GroupBy(_ => 1)    // group all rows into one group to get aggregates
            .Select(g => new
            {
                Total       = g.Count(),
                Active      = g.Count(r => r.Status == RecipeStatus.Active),
                AvgMargin   = g.Average(r => (decimal?)r.MarginPercent)  // nullable avg (no rows = null)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var purchaseTotalTask = db.Purchases
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.PurchasedAt >= thirtyDaysAgo)
            .SumAsync(p => p.TotalCost, cancellationToken);

        var recentPurchasesTask = db.Purchases
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Include(p => p.Ingredient)
            .OrderByDescending(p => p.PurchasedAt)
            .Take(5)
            .Select(p => new RecentPurchaseItem(
                p.Ingredient!.Name,
                p.TotalCost,
                p.PurchasedAt))
            .ToListAsync(cancellationToken);

        // Wait for all four queries to complete
        await Task.WhenAll(ingredientsTask, recipeSummaryTask, purchaseTotalTask, recentPurchasesTask);

        // ── Assemble the response ──────────────────────────────────

        var ingredients = await ingredientsTask;
        var lowStock = ingredients
            .Where(i => i.ReorderThreshold.HasValue && i.StockQuantity < i.ReorderThreshold.Value)
            .Select(i => new LowStockItem(i.Id, i.Name, i.StockQuantity, i.ReorderThreshold!.Value))
            .ToList();

        var recipeStats = await recipeSummaryTask;
        var purchaseTotal = await purchaseTotalTask;
        var recentPurchases = await recentPurchasesTask;

        return new DashboardResponse(
            new InventorySummary(
                ingredients.Count,
                lowStock.Count,
                lowStock),
            new RecipeSummary(
                recipeStats?.Total    ?? 0,
                recipeStats?.Active   ?? 0,
                recipeStats?.AvgMargin ?? null),
            new PurchaseSummary(
                purchaseTotal,
                recentPurchases));
    }
}
```

---

## Step 2 — Register the Endpoint

Create `src/Nastart.Api/Features/Dashboard/DashboardEndpoints.cs`:

```csharp
using MediatR;

namespace Nastart.Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this WebApplication app)
    {
        app.MapGet("/dashboard", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetDashboardSummaryQuery(), ct);
            return TypedResults.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetDashboard")
        .WithSummary("Business overview: inventory, recipes, recent spend")
        .WithTags("Dashboard");
    }
}
```

Register in `Program.cs`:

```csharp
app.MapDashboardEndpoints();
```

---

## ✅ Test It

```http
GET http://localhost:5000/dashboard
Authorization: Bearer {{token}}
```

Expected response:

```json
{
  "inventory": {
    "totalIngredients": 4,
    "lowStockCount": 1,
    "lowStockIngredients": [
      {
        "ingredientId": "...",
        "name": "Yeast",
        "stockQuantity": 0.05,
        "reorderThreshold": 0.5
      }
    ]
  },
  "recipes": {
    "totalRecipes": 2,
    "activeRecipes": 1,
    "avgMarginPercent": 62.34
  },
  "purchases": {
    "last30DaysTotal": 248.75,
    "recentPurchases": [
      { "ingredientName": "Bread Flour", "totalCost": 13.50, "purchasedAt": "2026-03-09T..." }
    ]
  }
}
```

---

## How Task.WhenAll Works

```csharp
// This starts all four queries immediately — they run concurrently
var ingredientsTask = db.Ingredients.Where(...).ToListAsync();  // fires immediately
var recipesTask     = db.Recipes.Where(...).FirstOrDefaultAsync();  // fires immediately
var totalTask       = db.Purchases.SumAsync();  // fires immediately
var recentTask      = db.Purchases.Take(5).ToListAsync();  // fires immediately

// This waits until ALL four have finished (or one throws)
await Task.WhenAll(ingredientsTask, recipesTask, totalTask, recentTask);
```

Compare to sequential (do NOT do this for independent queries):

```csharp
// Sequential — each waits for the previous to finish  ← SLOW
var ingredients = await db.Ingredients.Where(...).ToListAsync();
var recipes     = await db.Recipes.Where(...).FirstOrDefaultAsync();
var total       = await db.Purchases.SumAsync();
var recent      = await db.Purchases.Take(5).ToListAsync();
```

> **Note**: `Task.WhenAll` throws if any task faults. The exception from the first faulted task is re-thrown. With EF Core and a single `DbContext`, all parallel queries share one connection pool — this is safe and idiomatic.

---

## The GroupBy Aggregate Pattern

```csharp
db.Recipes.Where(r => r.UserId == userId)
    .GroupBy(_ => 1)   // put ALL rows in one group (value doesn't matter)
    .Select(g => new {
        Total  = g.Count(),
        Active = g.Count(r => r.Status == RecipeStatus.Active),
        Avg    = g.Average(r => (decimal?)r.MarginPercent)
    })
    .FirstOrDefaultAsync()
```

This generates a single SQL query:

```sql
SELECT COUNT(*), COUNT(CASE WHEN status = 'Active' THEN 1 END), AVG(margin_percent)
FROM   recipes
WHERE  user_id = @userId
```

Without `GroupBy(_ => 1)`, you would need three separate queries to get `COUNT`, conditional `COUNT`, and `AVG`.

---

## Key Concepts

| Term | Definition |
|---|---|
| **`Task.WhenAll`** | Starts all tasks in parallel and awaits all of them. Faster than `await` each one sequentially. |
| **Independent queries** | Queries where the result of one is not needed to build another. These are candidates for `Task.WhenAll`. |
| **`GroupBy(_ => 1)`** | Groups all rows into one group to compute multiple aggregates (COUNT, AVG, etc.) in one SQL query. |
| **`(decimal?)` cast** | Makes `.Average()` return `null` instead of throwing when there are no rows. |
| **Write-back cache (recap)** | `margin_percent` on recipes was pre-computed in Lesson 25. The dashboard reads it directly — no recalculation. |
| **`SumAsync` with no rows** | Returns `0` (not null). For total spend, this is the correct default. |

---

## Phase 5 Complete

You have built the full Nastart application:

| Lesson | Feature |
|--------|---------|
| 18 | Schema migration — users + categories |
| 19 | User registration with BCrypt |
| 20 | JWT login |
| 21 | Protect endpoints with `ICurrentUser` |
| 22 | Ingredients full schema + categories |
| 23 | Price history + spike notifications |
| 24 | Recipes + status state machine |
| 25 | Recipe costing + margin |
| 26 | Record purchase with transaction |
| 27 | Paginated purchase history |
| 28 | Dashboard with parallel queries |

**What comes next** (Phase 6 — Telegram Bot + OCR):
- Connect the Telegram bot to this API
- Use PaddleOCR to scan receipts and pre-fill the `RecordPurchase` form
- Send `PriceSpikeNotification` to the user's Telegram chat
- Bot commands for quick recipe cost checks on the go
