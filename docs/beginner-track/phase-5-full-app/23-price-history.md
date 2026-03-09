# Lesson 23: Price History — Tracking Every Price Change and Detecting Spikes

> **Phase**: 5 — Building the Full Application
> **Prerequisite**: Lesson 22. The `ingredients` table has `user_id` and `price_per_unit`.
> **What you build**: `PATCH /ingredients/{id}/price` — saves the new price, records the old price in `price_history`, and publishes a MediatR notification if the price jumped more than the user's alert threshold.

---

## What You Will Learn

- How to create a `price_history` table via EF Core migration
- How to use `INotification` and `INotificationHandler` in MediatR
- The difference between a MediatR **Request** (one handler, returns a result) and a **Notification** (zero or more handlers, fire-and-forget)

---

## Why Price History Matters

Ingredient prices fluctuate. You want to:
1. **Audit** every price change so you can spot trends
2. **Alert** when a price spikes — so you can re-cost your recipes before selling at a loss

---

## The MediatR Notification Pattern

So far you have used `IRequest<TResponse>` — one handler, one result. Notifications are different:

```
Request → exactly one handler → returns result
Notification → zero or more handlers → no return value (fire-and-forget)
```

```python
# Python analogy: like emitting an event on an EventEmitter vs calling a function
event_bus.emit("price_spiked", ingredient_id=id, spike_pct=25)
```

A `PriceSpikeNotification` is published from inside the price update handler. Any number of handlers can react to it — today it's just a logger; later you can add Telegram, email, etc.

---

## Step 1 — Create the PriceHistory Entity

Create `src/Nastart.Api/Features/Ingredients/PriceHistory.cs`:

```csharp
namespace Nastart.Api.Features.Ingredients;

public sealed class PriceHistory
{
    public Guid           Id             { get; set; }
    public Guid           IngredientId   { get; set; }
    public decimal        OldPrice       { get; set; }
    public decimal        NewPrice       { get; set; }
    public decimal        ChangePercent  { get; set; }   // positive = increase, negative = decrease
    public DateTimeOffset RecordedAt     { get; set; }

    // Navigation
    public Ingredient? Ingredient { get; set; }
}
```

Create `src/Nastart.Api/Features/Ingredients/PriceHistoryConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nastart.Api.Features.Ingredients;

public sealed class PriceHistoryConfiguration : IEntityTypeConfiguration<PriceHistory>
{
    public void Configure(EntityTypeBuilder<PriceHistory> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.OldPrice).HasPrecision(12, 4);
        builder.Property(p => p.NewPrice).HasPrecision(12, 4);
        builder.Property(p => p.ChangePercent).HasPrecision(7, 4);

        // Cascade: if ingredient deleted, delete its history too
        builder.HasOne(p => p.Ingredient)
               .WithMany()
               .HasForeignKey(p => p.IngredientId)
               .OnDelete(DeleteBehavior.Cascade);

        // Index for fast per-ingredient history queries
        builder.HasIndex(p => p.IngredientId);
    }
}
```

Add the `DbSet` to `NastartDbContext`:

```csharp
public DbSet<PriceHistory> PriceHistory => Set<PriceHistory>();
```

---

## Step 2 — Create the Notification and Its Handler

Create `src/Nastart.Api/Features/Ingredients/PriceSpikeNotification.cs`:

```csharp
using MediatR;
using Microsoft.Extensions.Logging;

namespace Nastart.Api.Features.Ingredients;

// ── Notification (no return value) ────────────────────────────────

public sealed record PriceSpikeNotification(
    Guid    IngredientId,
    string  IngredientName,
    decimal OldPrice,
    decimal NewPrice,
    decimal ChangePercent)
    : INotification;

// ── Handler ────────────────────────────────────────────────────────

public sealed class PriceSpikeHandler(ILogger<PriceSpikeHandler> logger)
    : INotificationHandler<PriceSpikeNotification>
{
    public Task Handle(PriceSpikeNotification notification, CancellationToken cancellationToken)
    {
        // Phase 5: just log it.
        // Phase 6 (Telegram) will add a TelegramPriceSpikeHandler alongside this one.
        logger.LogWarning(
            "PRICE SPIKE: {Name} changed {Pct:+0.##;-0.##}% " +
            "(was {Old:C4}, now {New:C4})",
            notification.IngredientName,
            notification.ChangePercent,
            notification.OldPrice,
            notification.NewPrice);

        return Task.CompletedTask;
    }
}
```

> **`INotificationHandler` vs `IRequestHandler`**: notification handlers must return `Task` (no `<TResult>`), and MediatR calls all of them in parallel. If you add a second handler later, both fire automatically — no changes needed to the publisher.

---

## Step 3 — Create UpdateIngredientPrice Handler

Create `src/Nastart.Api/Features/Ingredients/UpdateIngredientPrice.cs`:

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Services;

namespace Nastart.Api.Features.Ingredients;

// ── Command ────────────────────────────────────────────────────────

public sealed record UpdateIngredientPriceCommand(Guid IngredientId, decimal NewPrice)
    : IRequest<IngredientResponse>;

// ── Validator ──────────────────────────────────────────────────────

public sealed class UpdateIngredientPriceValidator : AbstractValidator<UpdateIngredientPriceCommand>
{
    public UpdateIngredientPriceValidator()
    {
        RuleFor(x => x.NewPrice).GreaterThan(0);
    }
}

// ── Domain Exception ───────────────────────────────────────────────

public sealed class IngredientNotFoundException(Guid id)
    : Exception($"Ingredient {id} not found.");

// ── Handler ────────────────────────────────────────────────────────

public sealed class UpdateIngredientPriceHandler(
    NastartDbContext db,
    ICurrentUser currentUser,
    IPublisher publisher)                // IPublisher = MediatR's way to publish notifications
    : IRequestHandler<UpdateIngredientPriceCommand, IngredientResponse>
{
    // Price spike alert threshold: the user's MinMarginPercent drives this.
    // For simplicity in Phase 5, a fixed 10% threshold triggers the notification.
    private const decimal SpikeThresholdPercent = 10m;

    public async Task<IngredientResponse> Handle(
        UpdateIngredientPriceCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Load the ingredient (must belong to the current user)
        var ingredient = await db.Ingredients
            .Include(i => i.Category)
            .FirstOrDefaultAsync(
                i => i.Id == request.IngredientId && i.UserId == currentUser.UserId,
                cancellationToken);

        if (ingredient is null)
            throw new IngredientNotFoundException(request.IngredientId);

        var oldPrice = ingredient.PricePerUnit;
        var newPrice = request.NewPrice;

        // 2. Record the old price in history before changing it
        var changePercent = oldPrice == 0
            ? 100m
            : Math.Round((newPrice - oldPrice) / oldPrice * 100, 4);

        db.PriceHistory.Add(new PriceHistory
        {
            Id           = Guid.NewGuid(),
            IngredientId = ingredient.Id,
            OldPrice     = oldPrice,
            NewPrice     = newPrice,
            ChangePercent = changePercent,
            RecordedAt   = DateTimeOffset.UtcNow
        });

        // 3. Update the ingredient's current price
        ingredient.PricePerUnit  = newPrice;
        ingredient.LastUpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        // 4. Publish spike notification AFTER saving (outside the DB transaction)
        //    If the notification handler fails, the price update is already committed.
        if (Math.Abs(changePercent) >= SpikeThresholdPercent)
        {
            await publisher.Publish(new PriceSpikeNotification(
                ingredient.Id,
                ingredient.Name,
                oldPrice,
                newPrice,
                changePercent),
                cancellationToken);
        }

        return new IngredientResponse(
            ingredient.Id,
            ingredient.Name,
            ingredient.Unit,
            ingredient.PricePerUnit,
            ingredient.CategoryId,
            ingredient.Category?.Name,
            ingredient.StockQuantity,
            ingredient.ReorderThreshold,
            ingredient.ReorderThreshold.HasValue
                && ingredient.StockQuantity < ingredient.ReorderThreshold.Value);
    }
}
```

---

## Step 4 — Add Price History Query

Create `src/Nastart.Api/Features/Ingredients/GetPriceHistory.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Services;

namespace Nastart.Api.Features.Ingredients;

public sealed record GetPriceHistoryQuery(Guid IngredientId)
    : IRequest<List<PriceHistoryResponse>>;

public sealed record PriceHistoryResponse(
    Guid            Id,
    decimal         OldPrice,
    decimal         NewPrice,
    decimal         ChangePercent,
    DateTimeOffset  RecordedAt);

public sealed class GetPriceHistoryHandler(NastartDbContext db, ICurrentUser currentUser)
    : IRequestHandler<GetPriceHistoryQuery, List<PriceHistoryResponse>>
{
    public async Task<List<PriceHistoryResponse>> Handle(
        GetPriceHistoryQuery request,
        CancellationToken cancellationToken)
    {
        // Verify the ingredient belongs to the current user before returning history
        var ingredientExists = await db.Ingredients
            .AnyAsync(i => i.Id == request.IngredientId && i.UserId == currentUser.UserId,
                      cancellationToken);

        if (!ingredientExists)
            throw new IngredientNotFoundException(request.IngredientId);

        return await db.PriceHistory
            .AsNoTracking()
            .Where(p => p.IngredientId == request.IngredientId)
            .OrderByDescending(p => p.RecordedAt)
            .Select(p => new PriceHistoryResponse(
                p.Id, p.OldPrice, p.NewPrice, p.ChangePercent, p.RecordedAt))
            .ToListAsync(cancellationToken);
    }
}
```

---

## Step 5 — Add Routes to IngredientsEndpoints

```csharp
// PATCH /ingredients/{id}/price
group.MapPatch("/{id}/price", async (
    Guid id,
    UpdateIngredientPriceRequest body,
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(
        new UpdateIngredientPriceCommand(id, body.NewPrice), ct);
    return TypedResults.Ok(result);
})
.WithName("UpdateIngredientPrice")
.WithSummary("Update ingredient price and record history");

// GET /ingredients/{id}/price-history
group.MapGet("/{id}/price-history", async (
    Guid id,
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(new GetPriceHistoryQuery(id), ct);
    return TypedResults.Ok(result);
})
.WithName("GetPriceHistory")
.WithSummary("Get price history for an ingredient");

// The request body type (minimal — just the new price)
internal sealed record UpdateIngredientPriceRequest(decimal NewPrice);
```

---

## Step 6 — Generate the Migration

```bash
dotnet ef migrations add AddPriceHistory --project src/Nastart.Api
dotnet ef database update --project src/Nastart.Api
```

Verify the migration creates `price_history` table and its index.

---

## ✅ Test It

```http
### Update a price (>10% increase triggers the spike notification)
PATCH http://localhost:5000/ingredients/{{ingredientId}}/price
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "newPrice": 2.50
}

### View the price history
GET http://localhost:5000/ingredients/{{ingredientId}}/price-history
Authorization: Bearer {{token}}
```

Check your application logs — a `LogWarning` entry for the price spike should appear if the change was 10%+:

```
warn: PRICE SPIKE: Bread Flour changed +108.33% (was $1.2000, now $2.5000)
```

---

## Key Concepts

| Term | Definition |
|---|---|
| **`INotification`** | A MediatR message with no return value. Published with `IPublisher.Publish()`. |
| **`INotificationHandler<T>`** | Reacts to a notification. Multiple handlers can react to the same notification. |
| **Fire-and-forget** | The publisher doesn't need to wait for handlers to finish (though `await Publish()` does await them by default). |
| **Publish after SaveChanges** | Ensures the DB write succeeds before alerting. Avoids sending alerts for rolled-back writes. |
| **`IngredientNotFoundException` with 404** | Add this to the global exception handler: `IngredientNotFoundException => 404 Not Found`. |
| **Ownership check** | `WHERE user_id = @userId` on every query — a user cannot view OR modify another user's data. |

---

## Add IngredientNotFoundException to the Global Exception Handler

```csharp
IngredientNotFoundException => new ProblemDetails
{
    Title  = "Ingredient not found",
    Status = 404,
    Detail = ex.Message
},
```

---

## Next

Lesson 24 → **Recipes** — `POST /recipes` creates a recipe with a list of `RecipeIngredient` links. You will learn about junction tables, status state machines, and validating that referenced ingredients exist.
