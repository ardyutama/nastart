# Lesson 17: Business Queries — Nastart's Core Data Operations

> **Phase**: 4 — Database Mastery
> **Track**: Beginner (.NET 10 for Python/JS developers)
> **Prerequisite**: Lesson 16 (Efficient Queries) — you know how to write queries correctly

---

## What You Will Learn

Six concrete business queries that power the Nastart application — written as proper MediatR handlers using every technique from the previous lessons:

1. **`GetRecipeCostQuery`** — calculate the total ingredient cost of a recipe
2. **`GetProfitSummaryQuery`** — monthly revenue, expenses, and net profit
3. **`GetLowStockIngredientsQuery`** — stock items that need replenishing
4. **`GetPriceHistoryQuery`** — ingredient price trend with spike detection
5. **`GetPurchaseHistoryQuery`** — paginated purchase log
6. **`GetDashboardSummaryQuery`** — combined dashboard data in one round trip

---

## Query 1: GetRecipeCost

### What it does

Given a `recipeId`, calculate the current cost to make one batch:
- Load the recipe's ingredient lines
- Multiply each ingredient's quantity × current_price
- Sum to get total cost
- Compare against stored `total_cost` to see if a recalculation is needed

### Schema involved

```
RECIPE → RECIPE_INGREDIENT → INGREDIENT
```

### Implementation

```csharp
// Features/Recipes/GetRecipeCost.cs

public sealed record GetRecipeCostQuery(Guid RecipeId, Guid UserId)
    : IRequest<RecipeCostResponse?>;

public sealed record IngredientCostLine(
    Guid IngredientId,
    string IngredientName,
    string Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineCost);         // Quantity × UnitPrice

public sealed record RecipeCostResponse(
    Guid RecipeId,
    string RecipeName,
    decimal TotalCost,
    decimal SellPrice,
    decimal MarginPercent,
    int YieldQuantity,
    string YieldUnit,
    decimal CostPerUnit,       // TotalCost / YieldQuantity
    bool CostOutdated,         // true if current cost differs from stored total_cost
    List<IngredientCostLine> Lines);

public sealed class GetRecipeCostHandler
    : IRequestHandler<GetRecipeCostQuery, RecipeCostResponse?>
{
    private readonly NastartDbContext _db;

    public GetRecipeCostHandler(NastartDbContext db) => _db = db;

    public async Task<RecipeCostResponse?> Handle(
        GetRecipeCostQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Load recipe with its ingredient lines + the ingredient details
        var recipe = await _db.Recipes
            .AsNoTracking()
            .Where(r => r.Id == request.RecipeId && r.UserId == request.UserId)
            .Include(r => r.Ingredients)                   // RecipeIngredient rows
                .ThenInclude(ri => ri.Ingredient)          // actual Ingredient entity
            .FirstOrDefaultAsync(cancellationToken);

        if (recipe is null) return null;

        // 2. Calculate live cost lines using current ingredient prices
        var lines = recipe.Ingredients
            .Select(ri => new IngredientCostLine(
                ri.IngredientId,
                ri.Ingredient.Name,
                ri.Ingredient.Unit,
                ri.Quantity,
                ri.Ingredient.CurrentPrice,
                ri.Quantity * ri.Ingredient.CurrentPrice))   // ← cost calculation
            .ToList();

        var liveTotalCost = lines.Sum(l => l.LineCost);
        var costPerUnit = recipe.YieldQuantity > 0
            ? liveTotalCost / recipe.YieldQuantity
            : liveTotalCost;

        // 3. Detect if stored total_cost is outdated (prices changed since last save)
        var costOutdated = Math.Abs(liveTotalCost - recipe.TotalCost) > 0.01m;

        return new RecipeCostResponse(
            recipe.Id,
            recipe.Name,
            liveTotalCost,
            recipe.SellPrice,
            recipe.SellPrice > 0
                ? (recipe.SellPrice - liveTotalCost) / recipe.SellPrice * 100
                : 0,
            recipe.YieldQuantity,
            recipe.YieldUnit,
            costPerUnit,
            costOutdated,
            lines);
    }
}
```

### Endpoint

```csharp
// Features/Recipes/RecipesEndpoints.cs
app.MapGet("/recipes/{recipeId}/cost", async (
    Guid recipeId,
    Guid userId,           // from auth/header in real app
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(new GetRecipeCostQuery(recipeId, userId), ct);
    return result is null ? TypedResults.NotFound() : TypedResults.Ok(result);
})
.WithName("GetRecipeCost")
.WithSummary("Calculate the current ingredient cost of a recipe");
```

### Why `.Include().ThenInclude()` here

This is one of the cases where eager loading is justified: you **always** need the ingredient data with the recipe for cost calculation. Loading recipe alone then looping would cause N+1.

---

## Query 2: GetProfitSummary

### What it does

Monthly P&L for the dashboard:
- Total revenue from sales (quantity × recipe sell_price)
- Total estimated ingredient cost (quantity × recipe total_cost)
- Net profit = revenue − cost

### Schema involved

```
SALE → RECIPE
```

### Implementation

```csharp
// Features/Sales/GetProfitSummary.cs

public sealed record GetProfitSummaryQuery(Guid UserId, int Year, int Month)
    : IRequest<ProfitSummaryResponse>;

public sealed record RecipeProfitLine(
    string RecipeName,
    int UnitsSold,
    decimal Revenue,
    decimal TotalCost,
    decimal Profit,
    decimal MarginPercent);

public sealed record ProfitSummaryResponse(
    int Year,
    int Month,
    decimal TotalRevenue,
    decimal TotalCost,
    decimal NetProfit,
    decimal OverallMarginPercent,
    List<RecipeProfitLine> ByRecipe);

public sealed class GetProfitSummaryHandler
    : IRequestHandler<GetProfitSummaryQuery, ProfitSummaryResponse>
{
    private readonly NastartDbContext _db;

    public GetProfitSummaryHandler(NastartDbContext db) => _db = db;

    public async Task<ProfitSummaryResponse> Handle(
        GetProfitSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var startDate = new DateOnly(request.Year, request.Month, 1);
        var endDate = startDate.AddMonths(1);

        // Project directly into the response shape — no intermediate entities
        var byRecipe = await _db.Sales
            .AsNoTracking()
            .Where(s => s.UserId == request.UserId
                     && s.Date >= startDate
                     && s.Date < endDate)
            .GroupBy(s => new { s.RecipeId, s.Recipe.Name, s.Recipe.TotalCost, s.Recipe.SellPrice })
            .Select(g => new RecipeProfitLine(
                g.Key.Name,
                g.Sum(s => s.Quantity),
                g.Sum(s => s.Quantity * g.Key.SellPrice),        // revenue
                g.Sum(s => s.Quantity * g.Key.TotalCost),        // cost
                g.Sum(s => s.Profit),                            // profit (stored on Sale)
                g.Key.SellPrice > 0
                    ? (g.Key.SellPrice - g.Key.TotalCost) / g.Key.SellPrice * 100
                    : 0m))
            .OrderByDescending(r => r.Profit)
            .ToListAsync(cancellationToken);

        var totalRevenue = byRecipe.Sum(r => r.Revenue);
        var totalCost = byRecipe.Sum(r => r.TotalCost);
        var netProfit = byRecipe.Sum(r => r.Profit);

        return new ProfitSummaryResponse(
            request.Year,
            request.Month,
            totalRevenue,
            totalCost,
            netProfit,
            totalRevenue > 0 ? netProfit / totalRevenue * 100 : 0,
            byRecipe);
    }
}
```

### What SQL this generates

```sql
SELECT
    r.name,
    SUM(s.quantity)                AS units_sold,
    SUM(s.quantity * r.sell_price) AS revenue,
    SUM(s.quantity * r.total_cost) AS total_cost,
    SUM(s.profit)                  AS profit
FROM sales s
JOIN recipes r ON s.recipe_id = r.id
WHERE s.user_id = $1
  AND s.date >= $2
  AND s.date < $3
GROUP BY r.id, r.name, r.total_cost, r.sell_price
ORDER BY profit DESC
```

All aggregation happens in PostgreSQL — nothing is loaded into C# memory and summed.

---

## Query 3: GetLowStockIngredients

### What it does

Find all ingredients where `current_stock < min_stock`, sorted by how urgently they need replenishing.

You already saw the full implementation in Lesson 16. Here it is in its final MediatR handler form:

```csharp
// Features/Ingredients/GetLowStockIngredients.cs

public sealed record GetLowStockIngredientsQuery(Guid UserId)
    : IRequest<List<LowStockIngredientResponse>>;

public sealed record LowStockIngredientResponse(
    Guid Id,
    string Name,
    string Unit,
    decimal CurrentStock,
    decimal MinStock,
    decimal Deficit,           // MinStock - CurrentStock = how much short
    decimal StockRatio,        // CurrentStock / MinStock (0 = empty, 1 = at minimum)
    string CategoryName);

public sealed class GetLowStockIngredientsHandler
    : IRequestHandler<GetLowStockIngredientsQuery, List<LowStockIngredientResponse>>
{
    private readonly NastartDbContext _db;

    public GetLowStockIngredientsHandler(NastartDbContext db) => _db = db;

    public async Task<List<LowStockIngredientResponse>> Handle(
        GetLowStockIngredientsQuery request,
        CancellationToken cancellationToken)
    {
        return await _db.Ingredients
            .AsNoTracking()
            .Where(i => i.UserId == request.UserId
                     && i.CurrentStock < i.MinStock)          // server-side
            .Include(i => i.Category)                          // join for category name
            .OrderBy(i => i.CurrentStock / i.MinStock)         // most critical first (ratio 0 = critical)
            .Select(i => new LowStockIngredientResponse(
                i.Id,
                i.Name,
                i.Unit,
                i.CurrentStock,
                i.MinStock,
                i.MinStock - i.CurrentStock,
                i.MinStock > 0 ? i.CurrentStock / i.MinStock : 0m,
                i.Category.Name))
            .ToListAsync(cancellationToken);
    }
}
```

---

## Query 4: GetPriceHistory (with Spike Detection)

### What it does

- Return the price history for an ingredient over the past N days
- Detect price spikes (>15% increase vs the price before it)
- Used for the price trend chart and alert system

### Schema involved

```
PRICE_HISTORY where ingredient_id = X
```

### Implementation

```csharp
// Features/Ingredients/GetPriceHistory.cs

public sealed record GetPriceHistoryQuery(Guid IngredientId, Guid UserId, int Days = 90)
    : IRequest<PriceHistoryResponse?>;

public sealed record PricePoint(DateOnly Date, decimal Price, bool IsSpike, decimal ChangePercent);

public sealed record PriceHistoryResponse(
    Guid IngredientId,
    string IngredientName,
    string Unit,
    decimal CurrentPrice,
    decimal LowestPrice,
    decimal HighestPrice,
    decimal AveragePrice,
    List<PricePoint> History);

public sealed class GetPriceHistoryHandler
    : IRequestHandler<GetPriceHistoryQuery, PriceHistoryResponse?>
{
    private readonly NastartDbContext _db;

    public GetPriceHistoryHandler(NastartDbContext db) => _db = db;

    public async Task<PriceHistoryResponse?> Handle(
        GetPriceHistoryQuery request,
        CancellationToken cancellationToken)
    {
        // Verify the user owns this ingredient
        var ingredient = await _db.Ingredients
            .AsNoTracking()
            .Where(i => i.Id == request.IngredientId && i.UserId == request.UserId)
            .Select(i => new { i.Id, i.Name, i.Unit, i.CurrentPrice })
            .FirstOrDefaultAsync(cancellationToken);

        if (ingredient is null) return null;

        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-request.Days));

        // Load price points in chronological order
        var rawHistory = await _db.PriceHistory
            .AsNoTracking()
            .Where(ph => ph.IngredientId == request.IngredientId && ph.Date >= fromDate)
            .OrderBy(ph => ph.Date)
            .Select(ph => new { ph.Date, ph.Price })
            .ToListAsync(cancellationToken);

        if (rawHistory.Count == 0)
        {
            return new PriceHistoryResponse(
                ingredient.Id, ingredient.Name, ingredient.Unit,
                ingredient.CurrentPrice, ingredient.CurrentPrice,
                ingredient.CurrentPrice, ingredient.CurrentPrice,
                []);
        }

        // Spike detection — calculate in C# (this is a small dataset already loaded)
        // A spike is any increase >15% vs the previous price
        var history = rawHistory
            .Select((point, index) =>
            {
                if (index == 0)
                    return new PricePoint(point.Date, point.Price, false, 0);

                var prev = rawHistory[index - 1].Price;
                var changePercent = prev > 0 ? (point.Price - prev) / prev * 100 : 0;
                var isSpike = changePercent > 15;       // >15% = spike threshold

                return new PricePoint(point.Date, point.Price, isSpike, Math.Round(changePercent, 1));
            })
            .ToList();

        return new PriceHistoryResponse(
            ingredient.Id,
            ingredient.Name,
            ingredient.Unit,
            ingredient.CurrentPrice,
            rawHistory.Min(p => p.Price),
            rawHistory.Max(p => p.Price),
            rawHistory.Average(p => p.Price),
            history);
    }
}
```

> **Why spike detection in C#, not SQL?**
>
> Comparing each row to the previous row requires a window function (`LAG()`) in SQL, or loading the already-filtered, already-small dataset into C# and computing it there. For a 90-day history of one ingredient, the dataset is at most 90 rows — computing in C# is fine. The heavy lifting (filtering, ordering, limiting) already happened in SQL.

---

## Query 5: GetPurchaseHistory (Paginated)

### What it does

Returns a user's purchase history, paginated, with each purchase's line items.

### Schema involved

```
PURCHASE → PURCHASE_ITEM → INGREDIENT
```

### Implementation

```csharp
// Features/Purchases/GetPurchaseHistory.cs

public sealed record GetPurchaseHistoryQuery(Guid UserId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<PurchaseSummary>>;

public sealed record PurchaseItemSummary(
    string IngredientName,
    decimal Quantity,
    string Unit,
    decimal Price);

public sealed record PurchaseSummary(
    Guid Id,
    DateOnly PurchaseDate,
    string ShopName,
    decimal TotalAmount,
    string Status,
    List<PurchaseItemSummary> Items);

public sealed record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public sealed class GetPurchaseHistoryHandler
    : IRequestHandler<GetPurchaseHistoryQuery, PagedResult<PurchaseSummary>>
{
    private readonly NastartDbContext _db;

    public GetPurchaseHistoryHandler(NastartDbContext db) => _db = db;

    public async Task<PagedResult<PurchaseSummary>> Handle(
        GetPurchaseHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var baseQuery = _db.Purchases
            .AsNoTracking()
            .Where(p => p.UserId == request.UserId);

        // Count and data in parallel — two separate DB round trips
        var countTask = baseQuery.CountAsync(cancellationToken);

        var itemsTask = baseQuery
            .OrderByDescending(p => p.PurchaseDate)
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PurchaseSummary(
                p.Id,
                p.PurchaseDate,
                p.Shop != null ? p.Shop.Name : "Unknown",
                p.TotalAmount,
                p.Status,
                p.Items.Select(pi => new PurchaseItemSummary(
                    pi.Ingredient.Name,
                    pi.Quantity,
                    pi.Ingredient.Unit,
                    pi.Price)).ToList()))
            .ToListAsync(cancellationToken);

        await Task.WhenAll(countTask, itemsTask);   // run both DB queries in parallel

        var totalCount = countTask.Result;
        var items = itemsTask.Result;

        return new PagedResult<PurchaseSummary>(
            items,
            totalCount,
            request.Page,
            pageSize,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}
```

> **`Task.WhenAll(countTask, itemsTask)`** fires both database queries simultaneously. Since they don't depend on each other, there's no reason to wait for the count before sending the data query.

---

## Query 6: GetDashboardSummary

### What it does

One round trip that aggregates everything needed for the dashboard:
- Today's unread alert count
- Low stock ingredient count
- Current month revenue and profit
- 5 most recent purchases

### Why one query?

Individual queries for each dashboard widget would mean 4–5 separate database round trips per page load. Aggregating them reduces latency.

```csharp
// Features/Dashboard/GetDashboardSummary.cs

public sealed record GetDashboardSummaryQuery(Guid UserId) : IRequest<DashboardSummaryResponse>;

public sealed record DashboardSummaryResponse(
    int UnreadAlertCount,
    int LowStockCount,
    decimal MonthRevenue,
    decimal MonthProfit,
    List<RecentPurchaseItem> RecentPurchases);

public sealed record RecentPurchaseItem(
    Guid Id,
    DateOnly Date,
    string ShopName,
    decimal TotalAmount,
    int ItemCount);

public sealed class GetDashboardSummaryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryResponse>
{
    private readonly NastartDbContext _db;

    public GetDashboardSummaryHandler(NastartDbContext db) => _db = db;

    public async Task<DashboardSummaryResponse> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        // Fire all 4 queries in parallel — they are independent
        var alertCountTask = _db.Alerts
            .AsNoTracking()
            .CountAsync(a => a.UserId == request.UserId && !a.Read, cancellationToken);

        var lowStockCountTask = _db.Ingredients
            .AsNoTracking()
            .CountAsync(i => i.UserId == request.UserId
                          && i.CurrentStock < i.MinStock, cancellationToken);

        var monthStatsTask = _db.Sales
            .AsNoTracking()
            .Where(s => s.UserId == request.UserId
                     && s.Date >= monthStart
                     && s.Date < monthEnd)
            .GroupBy(_ => 1)   // group all into one aggregate row
            .Select(g => new
            {
                Revenue = g.Sum(s => s.Quantity * s.Recipe.SellPrice),
                Profit = g.Sum(s => s.Profit)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var recentPurchasesTask = _db.Purchases
            .AsNoTracking()
            .Where(p => p.UserId == request.UserId)
            .OrderByDescending(p => p.PurchaseDate)
            .Take(5)
            .Select(p => new RecentPurchaseItem(
                p.Id,
                p.PurchaseDate,
                p.Shop != null ? p.Shop.Name : "Unknown",
                p.TotalAmount,
                p.Items.Count))
            .ToListAsync(cancellationToken);

        // Wait for all 4 to complete
        await Task.WhenAll(alertCountTask, lowStockCountTask, monthStatsTask, recentPurchasesTask);

        var stats = monthStatsTask.Result;

        return new DashboardSummaryResponse(
            alertCountTask.Result,
            lowStockCountTask.Result,
            stats?.Revenue ?? 0m,
            stats?.Profit ?? 0m,
            recentPurchasesTask.Result);
    }
}
```

### Generated SQL (4 queries, all in parallel)

```sql
-- Query A (runs in parallel)
SELECT COUNT(*) FROM alerts WHERE user_id = $1 AND read = false;

-- Query B (runs in parallel)
SELECT COUNT(*) FROM ingredients WHERE user_id = $1 AND current_stock < min_stock;

-- Query C (runs in parallel)
SELECT SUM(s.quantity * r.sell_price), SUM(s.profit)
FROM sales s JOIN recipes r ON s.recipe_id = r.id
WHERE s.user_id = $1 AND s.date >= $2 AND s.date < $3;

-- Query D (runs in parallel)
SELECT p.id, p.purchase_date, sh.name, p.total_amount, COUNT(pi.id)
FROM purchases p
LEFT JOIN shops sh ON p.shop_id = sh.id
LEFT JOIN purchase_items pi ON pi.purchase_id = p.id
WHERE p.user_id = $1
ORDER BY p.purchase_date DESC
LIMIT 5;
```

---

## Putting It All Together — Endpoint Registration

```csharp
// Features/Dashboard/DashboardEndpoints.cs
public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/dashboard").WithTags("Dashboard");

        group.MapGet("/summary", async (Guid userId, IMediator mediator, CancellationToken ct) =>
            TypedResults.Ok(await mediator.Send(new GetDashboardSummaryQuery(userId), ct)))
            .WithName("GetDashboardSummary")
            .WithSummary("Get all dashboard metrics in one request");

        return app;
    }
}

// Register in Program.cs
app.MapDashboardEndpoints();
app.MapRecipesEndpoints();
app.MapPurchasesEndpoints();
```

---

## Business Query Patterns Summary

| Query | Key technique | Why |
|---|---|---|
| `GetRecipeCost` | `.Include().ThenInclude()` | Need ingredient prices with the recipe |
| `GetProfitSummary` | `.GroupBy().Select()` aggregate | All math happens in SQL |
| `GetLowStockIngredients` | `.Where()` server-side + `.Select()` | Filter and project at DB level |
| `GetPriceHistory` | Small result + C# spike logic | 90 rows max; window functions are overkill |
| `GetPurchaseHistory` | `.Skip().Take()` + `Task.WhenAll()` | Paginated; count and data in parallel |
| `GetDashboardSummary` | `Task.WhenAll()` for 4 parallel queries | One page load = one parallel batch |

---

## Alert Threshold Logic

The alert system is driven by the same queries. Here's where the thresholds live:

| Alert | Query check | Threshold |
|---|---|---|
| Price spike | Inside `UpdateIngredientPriceHandler` after save | New price > previous price × 1.15 |
| Inflation trend | Scheduled job using `GetPriceHistoryQuery` | Average price up >10% over 30 days |
| Low stock | `GetLowStockIngredientsQuery` result count > 0 | `current_stock < min_stock` |
| Margin danger | Inside `RecalculateRecipeCostsHandler` | `margin_percent < user.min_margin_percent` |

When a threshold is met, the handler publishes a MediatR notification:

```csharp
// Inside UpdateIngredientPriceHandler, after saving the new price
if (changePercent > 15)
{
    await _mediator.Publish(
        new PriceSpikeNotification(ingredient.Id, ingredient.UserId, previousPrice, newPrice),
        cancellationToken);
}
```

The notification handler creates the `Alert` row and (optionally) sends a Telegram message — keeping business logic decoupled from the detection logic.

---

## ✅ Run It — Test All Business Queries

```http
### Get recipe cost
GET /recipes/{recipeId}/cost?userId={userId}

### Get monthly profit
GET /sales/profit-summary?userId={userId}&year=2025&month=1

### Get low stock items
GET /ingredients/low-stock?userId={userId}

### Get price history for an ingredient
GET /ingredients/{ingredientId}/price-history?userId={userId}&days=90

### Get paginated purchase history
GET /purchases?userId={userId}&page=1&pageSize=20

### Get dashboard summary
GET /dashboard/summary?userId={userId}
```

---

## Key Concepts

| Term | Definition |
|---|---|
| **`GroupBy().Select()` aggregate** | Projects SQL `GROUP BY` + `SUM/COUNT` — all math stays in PostgreSQL |
| **`Task.WhenAll()`** | Runs multiple async operations in parallel — use for independent queries |
| **Spike detection** | Comparing current price to previous row — computed in C# after loading small dataset |
| **Parallel queries** | Dashboard fires 4 `CountAsync`/`ToListAsync` calls simultaneously |
| **`.Skip((page-1)*size).Take(size)`** | Translates to SQL `OFFSET` + `LIMIT` |
| **`PagedResult<T>`** | Wrapper response type: items + totalCount + page + pageSize + totalPages |
| **`Math.Clamp(value, min, max)`** | Safely clamps page size so users can't request 1,000,000 rows |
| **`DateOnly`** | .NET type for date-without-time — maps to PostgreSQL `date` column type |

---

## Phase 4 Complete

You have now completed all four database mastery lessons:

| Lesson | Topic |
|---|---|
| 14 | Normalization — why the schema looks the way it does |
| 15 | Indexing — which columns to index and how to verify it |
| 16 | Efficient queries — AsNoTracking, projection, server-side filters, pagination |
| 17 | Business queries — the real queries that power the Nastart application |

The full beginner track is now complete. You have built:
- A minimal API with full CRUD (Phase 1)
- A MediatR-based CQRS architecture (Phase 2)
- A Telegram bot that reuses your application layer (Phase 3)
- A production-quality database layer with normalization, indexes, and real business queries (Phase 4)
