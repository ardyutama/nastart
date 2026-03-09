# Lesson 16: Efficient LINQ and EF Core Queries

> **Phase**: 4 — Database Mastery
> **Track**: Beginner (.NET 10 for Python/JS developers)
> **Prerequisite**: Lesson 15 (Indexing) — you have indexes in place; now write queries that use them

---

## What You Will Learn

- The N+1 query problem and how to fix it with `.Include()`
- How `.Select()` projection avoids loading unnecessary data
- Why `.AsNoTracking()` matters for read-only queries
- Filtering server-side vs client-side (`.Where()` before `.ToListAsync()`)
- Pagination with `.Skip()` and `.Take()`
- How to debug what SQL EF Core actually generates
- When raw SQL is the right tool

---

## Setup — The DbContext We'll Use

All examples in this lesson assume this context:

```csharp
// NastartDbContext.cs (simplified)
public class NastartDbContext : DbContext
{
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<PriceHistory> PriceHistory => Set<PriceHistory>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<Alert> Alerts => Set<Alert>();
}
```

---

## Problem 1: The N+1 Query Problem

This is the most common EF Core performance bug for beginners. The name comes from: 1 query to get the list, then N queries to get the related data for each item.

### The problem — reading recipes without includes

```csharp
// ❌ N+1 Problem
var recipes = await db.Recipes
    .Where(r => r.UserId == userId && r.Status == "active")
    .ToListAsync();

foreach (var recipe in recipes)
{
    // EF Core lazy-loads here — ONE query per recipe!
    var ingredientCount = recipe.Ingredients.Count;
}
```

If you have 20 active recipes, this executes **21 SQL queries**:

```sql
-- Query 1: get all recipes
SELECT * FROM recipes WHERE user_id = $1 AND status = 'active'

-- Query 2: get ingredients for recipe 1
SELECT * FROM recipe_ingredients WHERE recipe_id = $1

-- Query 3: get ingredients for recipe 2
SELECT * FROM recipe_ingredients WHERE recipe_id = $2

-- ...20 more queries...
```

> EF Core needs Lazy Loading to be configured for this to happen automatically. In Nastart we don't enable it — so without includes you'd get `NullReferenceException` or empty collections instead. Still, the pattern is worth knowing.

### The fix — `.Include()` and `.ThenInclude()`

```csharp
// ✅ Eager loading — one query with JOIN
var recipes = await db.Recipes
    .Where(r => r.UserId == userId && r.Status == "active")
    .Include(r => r.Ingredients)                   // load RecipeIngredient rows
        .ThenInclude(ri => ri.Ingredient)          // load the actual Ingredient for each
    .ToListAsync();

// Now recipe.Ingredients is already populated — no extra queries
foreach (var recipe in recipes)
{
    var ingredientCount = recipe.Ingredients.Count;    // zero extra queries
    var totalCost = recipe.Ingredients.Sum(ri => ri.Cost);
}
```

Generated SQL (one query with JOINs):

```sql
SELECT r.*, ri.*, i.*
FROM recipes r
LEFT JOIN recipe_ingredients ri ON ri.recipe_id = r.id
LEFT JOIN ingredients i ON i.id = ri.ingredient_id
WHERE r.user_id = $1 AND r.status = 'active'
```

### When to use `.Include()`

Use it when you know you'll need the related data. Don't include everything — only what you actually use.

```csharp
// ❌ Over-including
.Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
.Include(r => r.Sales)             // you never use Sales in this query
.Include(r => r.User)              // you never use User here
```

```csharp
// ✅ Include only what you need
.Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
```

---

## Problem 2: Loading Full Entities When You Only Need a Few Columns

When listing ingredients, you probably only need `id`, `name`, `unit`, `current_price`, `current_stock`. But `SELECT *` loads `min_stock`, `last_purchase_date`, the full `category_id`, and navigation objects too.

### Bad: `.ToListAsync()` on full entity

```csharp
// ❌ Loads every column — 10+ columns per row
var ingredients = await db.Ingredients
    .Where(i => i.UserId == userId)
    .ToListAsync();

// Then you map it in C#
return ingredients.Select(i => new IngredientListItem(i.Id, i.Name, i.CurrentPrice)).ToList();
```

### Good: `.Select()` projection — SQL-level column reduction

```csharp
// ✅ Only the columns you need are fetched from the database
var ingredients = await db.Ingredients
    .Where(i => i.UserId == userId)
    .Select(i => new IngredientListItem(
        i.Id,
        i.Name,
        i.Unit,
        i.CurrentPrice,
        i.CurrentStock
    ))
    .ToListAsync();
```

Generated SQL:

```sql
-- ✅ Projection — minimal data over the wire
SELECT id, name, unit, current_price, current_stock
FROM ingredients
WHERE user_id = $1

-- vs
-- ❌ Full entity
SELECT id, user_id, category_id, name, unit, current_price, current_stock,
       min_stock, last_purchase_date
FROM ingredients
WHERE user_id = $1
```

### Projection with Include

You can also project related data inline:

```csharp
// Get recipe list with ingredient names (no full objects)
var recipes = await db.Recipes
    .Where(r => r.UserId == userId)
    .Select(r => new RecipeSummary(
        r.Id,
        r.Name,
        r.SellPrice,
        r.MarginPercent,
        r.Ingredients.Count,                        // just the count, not the objects
        r.Ingredients.Sum(ri => ri.Cost)            // aggregate in SQL
    ))
    .ToListAsync();
```

---

## Problem 3: Client-Side Filtering

### The mistake — calling `.ToList()` then filtering in C#

```csharp
// ❌ Fetches ALL ingredients from DB, then filters in C# (in memory)
var lowStock = (await db.Ingredients.ToListAsync())
    .Where(i => i.UserId == userId && i.CurrentStock < i.MinStock)
    .ToList();

// Python equivalent of what this does:
# rows = db.all()          # SELECT * FROM ingredients  ← 50,000 rows
# filtered = [r for r in rows if r.user_id == uid and r.stock < r.min]
```

### The fix — `.Where()` before `.ToListAsync()`

```csharp
// ✅ WHERE clause runs in PostgreSQL — only matching rows are sent to C#
var lowStock = await db.Ingredients
    .Where(i => i.UserId == userId && i.CurrentStock < i.MinStock)
    .ToListAsync();
```

Generated SQL:

```sql
SELECT * FROM ingredients
WHERE user_id = $1 AND current_stock < min_stock
```

**Rule**: Any LINQ operator that can be translated to SQL should appear *before* `.ToListAsync()`. If you call `.ToListAsync()` first, everything after it runs in C#.

---

## Technique 4: `.AsNoTracking()` for Read-Only Queries

By default, EF Core **tracks** every entity it loads. Tracking means the change tracker stores a snapshot of each entity so it can detect changes when you call `SaveChangesAsync()`.

For read-only queries (GET endpoints, queries, reports), you don't need tracking. Disabling it is faster and uses less memory.

```csharp
// ❌ Tracking overhead — unnecessary for reads
var ingredients = await db.Ingredients
    .Where(i => i.UserId == userId)
    .ToListAsync();

// ✅ No tracking — faster reads, less memory
var ingredients = await db.Ingredients
    .AsNoTracking()
    .Where(i => i.UserId == userId)
    .ToListAsync();
```

### When to use `.AsNoTracking()`

| Query type | Tracking needed? |
|---|---|
| GET list endpoint | No — use `.AsNoTracking()` |
| GET single endpoint | No — use `.AsNoTracking()` |
| Load entity to UPDATE it | Yes — tracker must detect changes |
| Load entity to DELETE it | Yes — tracker must know about it |

### Pattern: `AsNoTracking` on the DbContext level

For handlers that are purely queries, you can set `AsNoTracking` at the query level:

```csharp
// In GetIngredientsHandler.cs
public async Task<List<IngredientResponse>> Handle(...)
{
    return await db.Ingredients
        .AsNoTracking()                   // ← read-only signal
        .Where(i => i.UserId == request.UserId)
        .Select(i => new IngredientResponse(i.Id, i.Name, i.CurrentPrice, i.CurrentStock))
        .ToListAsync(cancellationToken);
}
```

---

## Technique 5: Pagination With `.Skip()` and `.Take()`

Never return all rows when the user is looking at a list. Use pagination.

```csharp
// ✅ Paginated query
public sealed record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);

var page = 1;      // from query string: ?page=1
var pageSize = 20; // from query string: ?pageSize=20

var totalCount = await db.Purchases
    .Where(p => p.UserId == userId)
    .CountAsync(cancellationToken);

var items = await db.Purchases
    .AsNoTracking()
    .Where(p => p.UserId == userId)
    .OrderByDescending(p => p.PurchaseDate)
    .Skip((page - 1) * pageSize)    // skip pages before this one
    .Take(pageSize)                  // take only this page's rows
    .Select(p => new PurchaseListItem(p.Id, p.PurchaseDate, p.TotalAmount, p.Status))
    .ToListAsync(cancellationToken);

var result = new PagedResult<PurchaseListItem>(items, totalCount, page, pageSize);
```

Generated SQL:

```sql
SELECT COUNT(*) FROM purchases WHERE user_id = $1;

SELECT id, purchase_date, total_amount, status
FROM purchases
WHERE user_id = $1
ORDER BY purchase_date DESC
LIMIT 20 OFFSET 0;   -- OFFSET = (page - 1) * pageSize
```

### Exposing pagination in the endpoint

```csharp
app.MapGet("/purchases", async (
    Guid userId,
    int page = 1,
    int pageSize = 20,
    IMediator mediator,
    CancellationToken ct) =>
{
    pageSize = Math.Clamp(pageSize, 1, 100);  // never allow > 100 rows per request
    var result = await mediator.Send(
        new GetPurchasesQuery(userId, page, pageSize), ct);
    return TypedResults.Ok(result);
});
```

---

## Technique 6: Debugging — What SQL Does EF Core Generate?

### Option A: `ToQueryString()` (development only)

```csharp
var query = db.Ingredients
    .AsNoTracking()
    .Where(i => i.UserId == userId && i.CurrentStock < i.MinStock)
    .Select(i => new { i.Id, i.Name, i.CurrentStock });

// Prints the SQL to inspect
var sql = query.ToQueryString();
Console.WriteLine(sql);
// → SELECT i.id, i.name, i.current_stock
//   FROM ingredients AS i
//   WHERE i.user_id = $1 AND i.current_stock < i.min_stock
```

### Option B: EF Core logging

In `appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

This logs every SQL query EF Core executes to the console — invaluable during development.

---

## Technique 7: Raw SQL for Complex Aggregations

Some queries are difficult or impossible to express cleanly in LINQ. PostgreSQL-specific aggregations, CTEs, window functions — write these as raw SQL.

```csharp
// Example: monthly revenue with window function
var monthlyStats = await db.Database
    .SqlQueryRaw<MonthlyStat>("""
        SELECT
            DATE_TRUNC('month', date) AS month,
            SUM(quantity * profit)   AS total_profit,
            COUNT(*)                 AS sale_count
        FROM sales
        WHERE user_id = {0}
        GROUP BY DATE_TRUNC('month', date)
        ORDER BY month DESC
        LIMIT 12
    """, userId)
    .ToListAsync(cancellationToken);
```

> **Security**: Use `{0}` parameterized placeholders, never string interpolation directly into SQL.
> `$"WHERE user_id = '{userId}'"` is SQL injection. `{0}` with EF Core uses parameterized queries automatically.

For updating data with raw SQL:

```csharp
// Bulk update — faster than loading + SaveChanges for mass updates
await db.Database.ExecuteSqlInterpolatedAsync(
    $"UPDATE ingredients SET current_price = {newPrice}, last_purchase_date = NOW() WHERE id = {ingredientId}",
    cancellationToken);
```

Or with EF Core 7+ `ExecuteUpdateAsync`:

```csharp
// ✅ EF Core 7+ bulk update (no load required)
await db.Ingredients
    .Where(i => i.Id == ingredientId)
    .ExecuteUpdateAsync(s => s
        .SetProperty(i => i.CurrentPrice, newPrice)
        .SetProperty(i => i.LastPurchaseDate, DateTime.UtcNow),
        cancellationToken);
```

---

## Full Pattern: Ideal Read Handler

Combining all these techniques into one handler:

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
    decimal Deficit);          // MinStock - CurrentStock

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
            .AsNoTracking()                                          // read-only
            .Where(i => i.UserId == request.UserId                   // server-side filter
                     && i.CurrentStock < i.MinStock)                 // server-side filter
            .OrderBy(i => i.CurrentStock / i.MinStock)               // most critical first
            .Select(i => new LowStockIngredientResponse(             // projection
                i.Id,
                i.Name,
                i.Unit,
                i.CurrentStock,
                i.MinStock,
                i.MinStock - i.CurrentStock))                        // computed in SQL
            .ToListAsync(cancellationToken);
    }
}
```

---

## Checklist — Before You Ship a Query Handler

```
[ ] Is .AsNoTracking() used on read-only queries?
[ ] Is .Where() applied before .ToListAsync()?
[ ] Is .Select() used to project only needed columns?
[ ] If joining related data, is .Include()/.ThenInclude() used?
[ ] If listing data, is pagination (.Skip/.Take) applied?
[ ] Is ToQueryString() checked during development to verify SQL?
[ ] No string concatenation in raw SQL (use {0} parameters)?
```

---

## Key Concepts

| Term | Definition |
|---|---|
| **N+1 problem** | 1 query for list + N queries for related data — solved with `.Include()` |
| **`.Include()`** | Eager-loads a navigation property using a SQL JOIN |
| **`.ThenInclude()`** | Chains `.Include()` to load a navigation of a navigation |
| **`.Select()` projection** | Reduces columns fetched — runs at SQL level |
| **`.AsNoTracking()`** | Skips EF Core change tracking — faster reads, less memory |
| **Client-side filtering** | Calling `.Where()` after `.ToListAsync()` — filter runs in C#, not SQL |
| **Server-side filtering** | Calling `.Where()` before `.ToListAsync()` — filter runs in PostgreSQL |
| **`.Skip().Take()`** | SQL `OFFSET` / `LIMIT` for pagination |
| **`ToQueryString()`** | Debug method that returns the SQL EF Core will generate |
| **`ExecuteUpdateAsync()`** | EF Core 7+ bulk update without loading entities |

---

## Next

Lesson 17 → **Business Queries** — apply everything you've learned to write the real, concrete business queries that power Nastart's dashboard, recipe costing, profit analysis, and alert system.
