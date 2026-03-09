# Lesson 26: Record Purchase — Atomic Multi-Table Writes with Transactions

> **Phase**: 5 — Building the Full Application
> **Prerequisite**: Lesson 25. The `ingredients` table has `stock_quantity` and `price_per_unit`.
> **What you build**: `POST /purchases` records buying an ingredient — updates stock, optionally updates the price, and saves the purchase record — all atomically inside one database transaction.

---

## What You Will Learn

- When to use a database transaction (multiple writes that must all succeed or all fail)
- How to use `IDbContextTransaction` in EF Core
- How to ensure rollback happens on failure

---

## Why Transactions Matter Here

Recording a purchase involves three writes:

1. Create a `purchases` row
2. Update `ingredients.stock_quantity += quantity_bought`
3. *(Optionally)* Update `ingredients.price_per_unit` to the new price paid

If step 2 succeeds but step 3 fails (or vice versa), your data is inconsistent — you have a purchase recorded but the ingredient stock or price is wrong.

A transaction wraps all three writes: either all commit together, or none of them do.

```python
# Python analogy (SQLAlchemy):
with db.begin():
    db.add(purchase)
    ingredient.stock_quantity += quantity
    ingredient.price_per_unit = new_price
# If any line raises, everything rolls back automatically
```

---

## The Schema

```sql
CREATE TABLE purchases (
    id                 UUID PRIMARY KEY,
    user_id            UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    ingredient_id      UUID NOT NULL REFERENCES ingredients(id) ON DELETE RESTRICT,
    quantity_bought    NUMERIC(12,3) NOT NULL,
    unit               TEXT NOT NULL,
    price_per_unit     NUMERIC(12,4) NOT NULL,
    total_cost         NUMERIC(12,4) NOT NULL,
    supplier           TEXT,
    receipt_image_url  TEXT,              -- stub for Phase 6 OCR
    notes              TEXT,
    purchased_at       TIMESTAMP WITH TIME ZONE NOT NULL
);
```

---

## Step 1 — Create the Purchase Entity

Create `src/Nastart.Api/Features/Purchases/Purchase.cs`:

```csharp
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Features.Purchases;

public sealed class Purchase
{
    public Guid           Id              { get; set; }
    public Guid           UserId          { get; set; }
    public Guid           IngredientId    { get; set; }
    public decimal        QuantityBought  { get; set; }
    public string         Unit            { get; set; } = string.Empty;
    public decimal        PricePerUnit    { get; set; }
    public decimal        TotalCost       { get; set; }
    public string?        Supplier        { get; set; }
    public string?        ReceiptImageUrl { get; set; }
    public string?        Notes           { get; set; }
    public DateTimeOffset PurchasedAt     { get; set; }

    // Navigation
    public User?       User       { get; set; }
    public Ingredient? Ingredient { get; set; }
}
```

Create `src/Nastart.Api/Features/Purchases/PurchaseConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nastart.Api.Features.Purchases;

public sealed class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Unit).IsRequired().HasMaxLength(50);
        builder.Property(p => p.QuantityBought).HasPrecision(12, 3);
        builder.Property(p => p.PricePerUnit).HasPrecision(12, 4);
        builder.Property(p => p.TotalCost).HasPrecision(12, 4);
        builder.Property(p => p.Supplier).HasMaxLength(200);
        builder.Property(p => p.Notes).HasMaxLength(1000);

        builder.HasOne(p => p.User)
               .WithMany()
               .HasForeignKey(p => p.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        // Restrict: do not delete an ingredient that has purchase history
        builder.HasOne(p => p.Ingredient)
               .WithMany()
               .HasForeignKey(p => p.IngredientId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.UserId, p.PurchasedAt });
    }
}
```

Add the `DbSet` to `NastartDbContext`:

```csharp
public DbSet<Purchase> Purchases => Set<Purchase>();
```

---

## Step 2 — RecordPurchaseCommand Handler

Create `src/Nastart.Api/Features/Purchases/RecordPurchase.cs`:

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Services;

namespace Nastart.Api.Features.Purchases;

// ── Command ────────────────────────────────────────────────────────

public sealed record RecordPurchaseCommand(
    Guid     IngredientId,
    decimal  QuantityBought,
    string   Unit,
    decimal  PricePerUnit,
    string?  Supplier,
    string?  Notes,
    DateTimeOffset? PurchasedAt)  // defaults to now if omitted
    : IRequest<PurchaseResponse>;

// ── Validator ──────────────────────────────────────────────────────

public sealed class RecordPurchaseValidator : AbstractValidator<RecordPurchaseCommand>
{
    public RecordPurchaseValidator()
    {
        RuleFor(x => x.QuantityBought).GreaterThan(0);
        RuleFor(x => x.PricePerUnit).GreaterThan(0);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Supplier).MaximumLength(200).When(x => x.Supplier is not null);
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}

// ── Response ───────────────────────────────────────────────────────

public sealed record PurchaseResponse(
    Guid           Id,
    Guid           IngredientId,
    string         IngredientName,
    decimal        QuantityBought,
    string         Unit,
    decimal        PricePerUnit,
    decimal        TotalCost,
    string?        Supplier,
    string?        Notes,
    DateTimeOffset PurchasedAt);

// ── Handler ────────────────────────────────────────────────────────

public sealed class RecordPurchaseHandler(NastartDbContext db, ICurrentUser currentUser)
    : IRequestHandler<RecordPurchaseCommand, PurchaseResponse>
{
    public async Task<PurchaseResponse> Handle(
        RecordPurchaseCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Load the ingredient — must belong to the current user
        var ingredient = await db.Ingredients
            .FirstOrDefaultAsync(
                i => i.Id == request.IngredientId && i.UserId == currentUser.UserId,
                cancellationToken);

        if (ingredient is null)
            throw new IngredientNotFoundException(request.IngredientId);

        // 2. Open a transaction — all three writes succeed or all roll back
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var purchasedAt = request.PurchasedAt ?? DateTimeOffset.UtcNow;
            var totalCost   = Math.Round(request.QuantityBought * request.PricePerUnit, 4);

            // Write A: Create the purchase record
            var purchase = new Purchase
            {
                Id             = Guid.NewGuid(),
                UserId         = currentUser.UserId,
                IngredientId   = ingredient.Id,
                QuantityBought = request.QuantityBought,
                Unit           = request.Unit,
                PricePerUnit   = request.PricePerUnit,
                TotalCost      = totalCost,
                Supplier       = request.Supplier,
                Notes          = request.Notes,
                PurchasedAt    = purchasedAt
            };
            db.Purchases.Add(purchase);

            // Write B: Update ingredient stock
            ingredient.StockQuantity += request.QuantityBought;

            // Write C: Update ingredient price to what was just paid
            //          (tracks current market price automatically)
            var oldPrice = ingredient.PricePerUnit;
            ingredient.PricePerUnit  = request.PricePerUnit;
            ingredient.LastUpdatedAt = DateTimeOffset.UtcNow;

            // Write D: Record price history entry if price changed
            if (oldPrice != request.PricePerUnit)
            {
                var changePercent = oldPrice == 0
                    ? 100m
                    : Math.Round((request.PricePerUnit - oldPrice) / oldPrice * 100, 4);

                db.PriceHistory.Add(new PriceHistory
                {
                    Id            = Guid.NewGuid(),
                    IngredientId  = ingredient.Id,
                    OldPrice      = oldPrice,
                    NewPrice      = request.PricePerUnit,
                    ChangePercent = changePercent,
                    RecordedAt    = purchasedAt
                });
            }

            // Flush all four writes in a single round-trip to the DB
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new PurchaseResponse(
                purchase.Id,
                ingredient.Id,
                ingredient.Name,
                purchase.QuantityBought,
                purchase.Unit,
                purchase.PricePerUnit,
                purchase.TotalCost,
                purchase.Supplier,
                purchase.Notes,
                purchase.PurchasedAt);
        }
        catch
        {
            // Any exception rolls back all four writes
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
```

---

## Step 3 — Register the Endpoint

Create `src/Nastart.Api/Features/Purchases/PurchasesEndpoints.cs`:

```csharp
using MediatR;

namespace Nastart.Api.Features.Purchases;

public static class PurchasesEndpoints
{
    public static void MapPurchasesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/purchases")
            .RequireAuthorization()
            .WithTags("Purchases");

        group.MapPost("/", async (RecordPurchaseCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return TypedResults.Created($"/purchases/{result.Id}", result);
        })
        .WithName("RecordPurchase")
        .WithSummary("Record buying an ingredient — updates stock and price automatically");
    }
}
```

Register in `Program.cs`:

```csharp
app.MapPurchasesEndpoints();
```

---

## Step 4 — Generate the Migration

```bash
dotnet ef migrations add AddPurchases --project src/Nastart.Api
dotnet ef database update --project src/Nastart.Api
```

---

## ✅ Test It

```http
### Record a purchase
POST http://localhost:5000/purchases
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "ingredientId": "{{flourId}}",
  "quantityBought": 10.0,
  "unit": "kg",
  "pricePerUnit": 1.35,
  "supplier": "City Mill Supplies",
  "notes": "Price went up — order earlier next time"
}
```

Verify three things in the database after the call:

```sql
-- 1. Purchase row created
SELECT * FROM purchases ORDER BY purchased_at DESC LIMIT 1;

-- 2. Ingredient stock increased and price updated
SELECT name, stock_quantity, price_per_unit FROM ingredients WHERE id = '{{flourId}}';

-- 3. Price history entry created (price changed from 1.20 to 1.35)
SELECT * FROM price_history WHERE ingredient_id = '{{flourId}}' ORDER BY recorded_at DESC LIMIT 1;
```

---

## Understanding `await using`

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(...);
```

`await using` ensures `DisposeAsync()` is called when the block exits — even if an exception is thrown. This releases the database connection back to the pool. Without it, connections leak.

The pattern is:

```
BeginTransaction → try { writes → Commit } catch { Rollback → throw }
```

If `CommitAsync` succeeds, the transaction is committed. If any write before it throws, the `catch` block calls `RollbackAsync`, which undoes all writes since `BeginTransaction`.

---

## Key Concepts

| Term | Definition |
|---|---|
| **Transaction** | A group of DB writes that succeed or fail together. Guarantees data consistency. |
| **`BeginTransactionAsync`** | Starts a transaction on the EF Core database connection. |
| **`CommitAsync`** | Persists all writes since `BeginTransaction`. |
| **`RollbackAsync`** | Undoes all writes since `BeginTransaction`. Called in the `catch` block. |
| **`await using`** | Ensures the transaction is disposed (and connection released) even if an exception occurs. |
| **`ON DELETE RESTRICT`** | Prevents deleting a purchased ingredient — you cannot lose your purchase history. |
| **Write D inside the transaction** | Price history is written atomically with the purchase — history is never missing for a purchase that changes the price. |

---

## Next

Lesson 27 → **Purchase History** — `GET /purchases` returns a paginated list of your purchases, with optional filters for ingredient and date range. You will build a reusable `PagedResult<T>` type.
