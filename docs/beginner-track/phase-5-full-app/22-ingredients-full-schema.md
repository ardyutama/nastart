# Lesson 22: Ingredients Full Schema — Adding User Ownership, Categories, and Stock

> **Phase**: 5 — Building the Full Application
> **Prerequisite**: Lesson 21. JWTs are validated and `ICurrentUser` is wired up.
> **What you build**: Migrate the `ingredients` table to the full schema — adding `user_id`, `category_id`, stock tracking fields, and updating all ingredient handlers.

---

## What You Will Learn

- How to write an additive migration that extends an existing table
- How to add a foreign key relationship (`CategoryId`) to an existing entity
- How to update your existing Create/Update handlers to accept new fields

---

## The Schema Change

The `ingredients` table in Phase 1–3 was minimal:

```sql
-- Phase 1-3 schema (simple)
CREATE TABLE ingredients (
    id           UUID PRIMARY KEY,
    name         TEXT NOT NULL,
    unit         TEXT NOT NULL,
    price_per_unit NUMERIC(12,4) NOT NULL
);
```

The full Nastart schema adds user ownership, category grouping, and stock management:

```sql
-- Phase 5 schema (full)
ALTER TABLE ingredients
    ADD COLUMN user_id           UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    ADD COLUMN category_id       UUID REFERENCES categories(id) ON DELETE SET NULL,
    ADD COLUMN stock_quantity    NUMERIC(12,3) NOT NULL DEFAULT 0,
    ADD COLUMN reorder_threshold NUMERIC(12,3),
    ADD COLUMN last_updated_at   TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now();
```

---

## Step 1 — Update the Ingredient Entity

Open `src/Nastart.Api/Features/Ingredients/Ingredient.cs` and add the new fields:

```csharp
namespace Nastart.Api.Features.Ingredients;

public sealed class Ingredient
{
    public Guid    Id              { get; set; }
    public string  Name            { get; set; } = string.Empty;
    public string  Unit            { get; set; } = string.Empty;
    public decimal PricePerUnit    { get; set; }

    // ── New in Phase 5 ────────────────────────────────────────────
    public Guid     UserId            { get; set; }
    public Guid?    CategoryId        { get; set; }
    public decimal  StockQuantity     { get; set; }
    public decimal? ReorderThreshold  { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties (EF Core uses these for JOINs)
    public User?     User     { get; set; }
    public Category? Category { get; set; }
}
```

---

## Step 2 — Update IngredientConfiguration

Open (or create) `src/Nastart.Api/Features/Ingredients/IngredientConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nastart.Api.Features.Ingredients;

public sealed class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Name).IsRequired().HasMaxLength(200);
        builder.Property(i => i.Unit).IsRequired().HasMaxLength(50);
        builder.Property(i => i.PricePerUnit).HasPrecision(12, 4);
        builder.Property(i => i.StockQuantity).HasPrecision(12, 3);
        builder.Property(i => i.ReorderThreshold).HasPrecision(12, 3);

        // Foreign key: user_id → users.id, cascade delete (delete user = delete their ingredients)
        builder.HasOne(i => i.User)
               .WithMany()
               .HasForeignKey(i => i.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        // Foreign key: category_id → categories.id, set null on delete
        builder.HasOne(i => i.Category)
               .WithMany(c => c.Ingredients)
               .HasForeignKey(i => i.CategoryId)
               .OnDelete(DeleteBehavior.SetNull);

        // Index for fast per-user queries (this is the most common query pattern)
        builder.HasIndex(i => i.UserId);
    }
}
```

---

## Step 3 — Update CreateIngredient Handler

Add the new fields to the command and handler:

```csharp
// Request
public sealed record CreateIngredientCommand(
    string   Name,
    string   Unit,
    decimal  PricePerUnit,
    Guid?    CategoryId,
    decimal  StockQuantity,
    decimal? ReorderThreshold)
    : IRequest<IngredientResponse>;

// Handler
public sealed class CreateIngredientHandler(
    NastartDbContext db,
    ICurrentUser currentUser)
    : IRequestHandler<CreateIngredientCommand, IngredientResponse>
{
    public async Task<IngredientResponse> Handle(
        CreateIngredientCommand request,
        CancellationToken cancellationToken)
    {
        var ingredient = new Ingredient
        {
            Id               = Guid.NewGuid(),
            Name             = request.Name,
            Unit             = request.Unit,
            PricePerUnit     = request.PricePerUnit,
            UserId           = currentUser.UserId,           // scoped to caller
            CategoryId       = request.CategoryId,
            StockQuantity    = request.StockQuantity,
            ReorderThreshold = request.ReorderThreshold,
            LastUpdatedAt    = DateTimeOffset.UtcNow
        };

        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync(cancellationToken);

        return MapToResponse(ingredient);
    }
}
```

---

## Step 4 — Update the IngredientResponse

The response should now include category and stock fields:

```csharp
public sealed record IngredientResponse(
    Guid     Id,
    string   Name,
    string   Unit,
    decimal  PricePerUnit,
    Guid?    CategoryId,
    string?  CategoryName,
    decimal  StockQuantity,
    decimal? ReorderThreshold,
    bool     IsLowStock);   // convenience flag: StockQuantity < ReorderThreshold
```

Update the `SELECT` projection in `GetIngredients` and `GetIngredientById`:

```csharp
.Select(i => new IngredientResponse(
    i.Id,
    i.Name,
    i.Unit,
    i.PricePerUnit,
    i.CategoryId,
    i.Category != null ? i.Category.Name : null,   // JOIN to categories
    i.StockQuantity,
    i.ReorderThreshold,
    i.ReorderThreshold.HasValue && i.StockQuantity < i.ReorderThreshold.Value))
```

> **`Category != null ? ...`**: this generates a `LEFT JOIN` in SQL. EF Core is smart enough to only join when you access the navigation property in the projection.

---

## Step 5 — Add Categories Endpoint (Read-Only)

Users need to know which category IDs exist when creating ingredients. Add a simple read-only endpoint.

Create `src/Nastart.Api/Features/Categories/CategoriesEndpoints.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Categories;

public sealed record GetCategoriesQuery : IRequest<List<CategoryResponse>>;
public sealed record CategoryResponse(Guid Id, string Name);

public sealed class GetCategoriesHandler(NastartDbContext db)
    : IRequestHandler<GetCategoriesQuery, List<CategoryResponse>>
{
    public Task<List<CategoryResponse>> Handle(
        GetCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        return db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryResponse(c.Id, c.Name))
            .ToListAsync(cancellationToken);
    }
}

public static class CategoriesEndpoints
{
    public static void MapCategoriesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/categories")
            .RequireAuthorization()
            .WithTags("Categories");

        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetCategoriesQuery(), ct);
            return TypedResults.Ok(result);
        })
        .WithName("GetCategories")
        .WithSummary("List all ingredient categories");
    }
}
```

Register it in `Program.cs`:

```csharp
app.MapCategoriesEndpoints();
```

---

## Step 6 — Generate the Migration

```bash
dotnet ef migrations add AddIngredientFullSchema --project src/Nastart.Api
dotnet ef database update --project src/Nastart.Api
```

EF Core generates `AddColumn` statements for each new property. Verify the generated migration file contains:

```
migrationBuilder.AddColumn<Guid>("user_id", ...)
migrationBuilder.AddColumn<Guid>("category_id", ...)
migrationBuilder.AddColumn<decimal>("stock_quantity", ...)
migrationBuilder.AddColumn<decimal>("reorder_threshold", ...)
migrationBuilder.AddColumn<DateTimeOffset>("last_updated_at", ...)
migrationBuilder.CreateIndex("IX_ingredients_user_id", ...)
migrationBuilder.AddForeignKey("FK_ingredients_users_user_id", ...)
migrationBuilder.AddForeignKey("FK_ingredients_categories_category_id", ...)
```

> **Existing rows**: If you have test data in the `ingredients` table and `user_id` is `NOT NULL`, the migration will fail. Either truncate the table first (`TRUNCATE ingredients CASCADE`) or temporarily make `UserId` nullable during migration, then make it required once the data is backfilled.

---

## ✅ Test It

```http
### Get categories (look up the seed GUIDs)
GET http://localhost:5000/categories
Authorization: Bearer {{token}}

### Create an ingredient with category and stock
POST http://localhost:5000/ingredients
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "name": "Bread Flour",
  "unit": "kg",
  "pricePerUnit": 1.20,
  "categoryId": "11111111-0000-0000-0000-000000000001",
  "stockQuantity": 25.0,
  "reorderThreshold": 5.0
}

### Get ingredients — only yours, includes category name + low stock flag
GET http://localhost:5000/ingredients
Authorization: Bearer {{token}}
```

---

## Key Concepts

| Term | Definition |
|---|---|
| **Additive migration** | A migration that only adds columns/tables, never drops. Safe to run on live databases. |
| **`ON DELETE CASCADE`** | When a user is deleted, all their ingredients are deleted too. |
| **`ON DELETE SET NULL`** | When a category is deleted, `category_id` becomes NULL — ingredient is preserved. |
| **`HasIndex(i => i.UserId)`** | Creates a B-tree index on `user_id`. Essential for `WHERE user_id = ...` performance. |
| **`LEFT JOIN` vs `INNER JOIN`** | Using `i.Category != null ?` in a projection generates a LEFT JOIN. All ingredients are returned, even those without a category. |
| **IsLowStock** | A computed field — not stored in the DB, derived from `StockQuantity < ReorderThreshold`. |

---

## Next

Lesson 23 → **Price History** — when you update an ingredient's price, the old price is saved to a `price_history` table and a `PriceSpikeNotification` is published via MediatR for any price jump over the threshold.
