# Lesson 27: Purchase History — Paginated Queries with Filters

> **Phase**: 5 — Building the Full Application
> **Prerequisite**: Lesson 26. The `purchases` table has rows.
> **What you build**: `GET /purchases` returns a paginated, filterable list of purchases. A reusable `PagedResult<T>` type is introduced that you will use in future list endpoints.

---

## What You Will Learn

- How to build a reusable pagination wrapper (`PagedResult<T>`)
- How to add query-string filters without loading all rows into memory
- How to write a `GetPurchaseDetailQuery` for a single purchase

---

## Why Pagination Matters

Without pagination, `GET /purchases` loads every purchase you have ever made — potentially thousands of rows. Pagination limits each response to a fixed page size and tells the client how many total pages exist:

```
GET /purchases?page=1&pageSize=20&ingredientId=...&from=2026-01-01
```

```python
# Python analogy (SQLAlchemy):
query.offset((page - 1) * page_size).limit(page_size)
```

---

## Step 1 — Create PagedResult

Create `src/Nastart.Api/Shared/Models/PagedResult.cs`:

```csharp
namespace Nastart.Api.Shared.Models;

/// <summary>
/// Generic pagination wrapper. Returned by any paginated list endpoint.
/// </summary>
public sealed record PagedResult<T>(
    List<T> Items,
    int     Page,
    int     PageSize,
    int     TotalCount)
{
    public int  TotalPages   => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage  => Page < TotalPages;
    public bool HasPrevPage  => Page > 1;
}
```

---

## Step 2 — GetPurchaseHistoryQuery

Create `src/Nastart.Api/Features/Purchases/GetPurchaseHistory.cs`:

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;
using Nastart.Api.Shared.Services;

namespace Nastart.Api.Features.Purchases;

// ── Query ──────────────────────────────────────────────────────────

public sealed record GetPurchaseHistoryQuery(
    int             Page,
    int             PageSize,
    Guid?           IngredientId,
    DateTimeOffset? From,
    DateTimeOffset? To)
    : IRequest<PagedResult<PurchaseResponse>>;

// ── Validator ──────────────────────────────────────────────────────

public sealed class GetPurchaseHistoryValidator : AbstractValidator<GetPurchaseHistoryQuery>
{
    public GetPurchaseHistoryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

// ── Handler ────────────────────────────────────────────────────────

public sealed class GetPurchaseHistoryHandler(NastartDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetPurchaseHistoryQuery, PagedResult<PurchaseResponse>>
{
    public async Task<PagedResult<PurchaseResponse>> Handle(
        GetPurchaseHistoryQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Start with a base query scoped to the current user
        var query = db.Purchases
            .AsNoTracking()
            .Where(p => p.UserId == currentUser.UserId);

        // 2. Apply optional filters — each is a conditional WHERE clause
        //    These are applied to the IQueryable<Purchase> and compose into a single SQL query
        if (request.IngredientId.HasValue)
            query = query.Where(p => p.IngredientId == request.IngredientId.Value);

        if (request.From.HasValue)
            query = query.Where(p => p.PurchasedAt >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(p => p.PurchasedAt <= request.To.Value);

        // 3. Count total matching rows (for TotalPages calculation)
        //    This runs SELECT COUNT(*) with the same WHERE clauses
        var totalCount = await query.CountAsync(cancellationToken);

        // 4. Fetch one page of results with a JOIN to get ingredient names
        var items = await query
            .Include(p => p.Ingredient)
            .OrderByDescending(p => p.PurchasedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new PurchaseResponse(
                p.Id,
                p.IngredientId,
                p.Ingredient!.Name,
                p.QuantityBought,
                p.Unit,
                p.PricePerUnit,
                p.TotalCost,
                p.Supplier,
                p.Notes,
                p.PurchasedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<PurchaseResponse>(items, request.Page, request.PageSize, totalCount);
    }
}
```

> **Two queries**: EF Core sends `COUNT(*)` first, then `SELECT ... LIMIT ... OFFSET ...`. This is a common and correct pattern — avoid loading all rows just to count them.

---

## Step 3 — GetPurchaseDetailQuery

Single-purchase lookup with full detail:

```csharp
public sealed record GetPurchaseDetailQuery(Guid PurchaseId) : IRequest<PurchaseResponse>;

public sealed class GetPurchaseDetailHandler(NastartDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetPurchaseDetailQuery, PurchaseResponse>
{
    public async Task<PurchaseResponse> Handle(
        GetPurchaseDetailQuery request,
        CancellationToken cancellationToken)
    {
        var purchase = await db.Purchases
            .AsNoTracking()
            .Include(p => p.Ingredient)
            .FirstOrDefaultAsync(
                p => p.Id == request.PurchaseId && p.UserId == currentUser.UserId,
                cancellationToken);

        if (purchase is null)
            throw new PurchaseNotFoundException(request.PurchaseId);

        return new PurchaseResponse(
            purchase.Id,
            purchase.IngredientId,
            purchase.Ingredient!.Name,
            purchase.QuantityBought,
            purchase.Unit,
            purchase.PricePerUnit,
            purchase.TotalCost,
            purchase.Supplier,
            purchase.Notes,
            purchase.PurchasedAt);
    }
}

public sealed class PurchaseNotFoundException(Guid id)
    : Exception($"Purchase {id} not found.");
```

---

## Step 4 — Add Routes to PurchasesEndpoints

Update `PurchasesEndpoints.cs`:

```csharp
// GET /purchases?page=1&pageSize=20&ingredientId=...&from=...&to=...
group.MapGet("/", async (
    IMediator mediator,
    CancellationToken ct,
    int page         = 1,
    int pageSize     = 20,
    Guid?           ingredientId = null,
    DateTimeOffset? from         = null,
    DateTimeOffset? to           = null) =>
{
    var result = await mediator.Send(
        new GetPurchaseHistoryQuery(page, pageSize, ingredientId, from, to), ct);
    return TypedResults.Ok(result);
})
.WithName("GetPurchaseHistory")
.WithSummary("Paginated purchase history with optional filters");

// GET /purchases/{id}
group.MapGet("/{id}", async (Guid id, IMediator mediator, CancellationToken ct) =>
{
    var result = await mediator.Send(new GetPurchaseDetailQuery(id), ct);
    return TypedResults.Ok(result);
})
.WithName("GetPurchaseDetail")
.WithSummary("Get a single purchase by ID");
```

Add `PurchaseNotFoundException` to the global exception handler:

```csharp
PurchaseNotFoundException => new ProblemDetails { Title = "Purchase not found", Status = 404, Detail = ex.Message },
```

---

## ✅ Test It

```http
### All purchases, newest first
GET http://localhost:5000/purchases
Authorization: Bearer {{token}}

### Paginate
GET http://localhost:5000/purchases?page=2&pageSize=5
Authorization: Bearer {{token}}

### Filter by ingredient
GET http://localhost:5000/purchases?ingredientId={{flourId}}
Authorization: Bearer {{token}}

### Filter by date range
GET http://localhost:5000/purchases?from=2026-01-01T00:00:00Z&to=2026-12-31T23:59:59Z
Authorization: Bearer {{token}}

### Single purchase
GET http://localhost:5000/purchases/{{purchaseId}}
Authorization: Bearer {{token}}
```

Expected response shape:

```json
{
  "items": [ { "id": "...", "ingredientName": "Bread Flour", ... } ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 42,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPrevPage": false
}
```

---

## How the SQL Looks

For `GET /purchases?ingredientId=...&from=2026-01-01&page=2&pageSize=5`:

```sql
-- COUNT query (runs first)
SELECT COUNT(*)
FROM   purchases
WHERE  user_id       = @userId
  AND  ingredient_id = @ingredientId
  AND  purchased_at >= @from;

-- Data query (runs second)
SELECT p.*, i.name AS ingredient_name
FROM   purchases p
JOIN   ingredients i ON i.id = p.ingredient_id
WHERE  p.user_id       = @userId
  AND  p.ingredient_id = @ingredientId
  AND  p.purchased_at >= @from
ORDER BY p.purchased_at DESC
LIMIT  5 OFFSET 5;   -- page 2, pageSize 5
```

---

## Key Concepts

| Term | Definition |
|---|---|
| **`PagedResult<T>`** | Generic wrapper that holds items + pagination metadata. Reusable for any list endpoint. |
| **`Skip / Take`** | EF Core generates `OFFSET` and `LIMIT` in SQL. `Skip((page-1) * pageSize).Take(pageSize)`. |
| **Composable `IQueryable`** | `WHERE` clauses added conditionally with `if (...) query = query.Where(...)` compose into a single SQL query. Nothing executes until `.ToListAsync()`. |
| **`CountAsync` before `ToListAsync`** | Two queries: one for count, one for data. More efficient than loading all rows and calling `.Count()`. |
| **PageSize max = 100** | Validation cap prevents clients from requesting 10,000 rows at once. |

---

## Next

Lesson 28 → **Dashboard** — `GET /dashboard` returns a business overview in one response using `Task.WhenAll()` to run multiple DB queries in parallel.
