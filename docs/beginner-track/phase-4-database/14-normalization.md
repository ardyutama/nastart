# Lesson 14: Database Normalization — Nastart Schema Analysis

> **Phase**: 4 — Database Mastery
> **Track**: Beginner (.NET 10 for Python/JS developers)
> **Prerequisite**: Lesson 04 (EF Core + PostgreSQL) — you already have `NastartDbContext` running

---

## What You Will Learn

- What normalization is and why it matters
- 1NF, 2NF, 3NF explained with real Nastart tables
- Why Nastart's schema makes the design decisions it does
- How to express normalized relationships in EF Core (`IEntityTypeConfiguration`)
- When *not* to normalize (intentional denormalization for performance)

---

## The Core Idea

Normalization is a set of rules to **reduce data duplication and prevent update anomalies**.

If you've worked with Python/pandas DataFrames or JavaScript objects, you've probably stacked duplicated data into arrays. Normalization is the discipline of extracting that into separate tables so you only store each fact *once*.

| Python/JS habit | Normalized SQL equivalent |
|---|---|
| `ingredient["category"] = "Dairy"` on every row | `CATEGORY` table, `category_id` FK on ingredient |
| `ingredient["price_history"] = [...]` list on object | Separate `PRICE_HISTORY` table |
| `recipe["ingredients"] = [{...}, {...}]` array | Junction table `RECIPE_INGREDIENT` |

---

## Normal Forms — Quick Reference

| Form | Rule | Violation looks like |
|---|---|---|
| **1NF** — First | Each column holds one atomic value. No lists, no arrays. | `ingredient.tags = "dairy,protein,frozen"` |
| **2NF** — Second | 1NF + every non-key column depends on the *whole* primary key (matters for composite PKs) | `purchase_item.ingredient_name` depends only on `ingredient_id`, not on `(purchase_id, ingredient_id)` |
| **3NF** — Third | 2NF + no column depends on *another non-key column* (no transitive dependency) | `ingredient.category_name` can be derived from `ingredient.category_id` — store one, not both |

---

## Walking Through Nastart's Schema

### 1NF Analysis — Category

**Violation (what NOT to do):**

```sql
-- BAD: comma-separated tags in one column — violates 1NF
CREATE TABLE ingredient (
    id UUID PRIMARY KEY,
    name TEXT,
    categories TEXT   -- "Dairy,Protein,Frozen" <- ATOMIC VIOLATION
);
```

```python
# equivalent Python anti-pattern you might be used to
ingredient = {"name": "Milk", "categories": "Dairy,Liquid"}
```

**Nastart solution — 1NF compliant:**

```
INGREDIENT
  id, user_id, category_id (FK), name, unit, ...

CATEGORY
  id, name, description
```

Each ingredient belongs to exactly one category. The category data lives in one place. If you rename "Dairy" to "Perishable", you update *one row* in `CATEGORY`, not a thousand rows in `INGREDIENT`.

---

### 3NF Analysis — Price History

**Violation (transitive dependency):**

```sql
-- BAD: current_price is a fact that changes over time.
-- Storing only the latest value makes history impossible to query.
-- But storing a price_on_date_x, price_on_date_y... is also a violation.

-- Worse: if category_name is stored on ingredient alongside category_id:
CREATE TABLE ingredient (
    id UUID PRIMARY KEY,
    category_id UUID,
    category_name TEXT,  -- ← transitive: depends on category_id, not on ingredient.id
    ...
);
```

```javascript
// equivalent JS anti-pattern
const ingredient = {
  id: "...",
  categoryId: "abc",
  categoryName: "Dairy"  // duplicated fact: already in categories table
};
```

**Nastart solution — 3NF compliant:**

```
INGREDIENT
  id, user_id, category_id, name, unit, current_price, current_stock, min_stock

PRICE_HISTORY
  id, ingredient_id (FK), date, price
```

`category_name` is never stored on `INGREDIENT`. You join to `CATEGORY` to get the name.

Price history is tracked in a dedicated table — not as multiple columns on `INGREDIENT` like `price_jan`, `price_feb`.

#### Intentional Denormalization: `current_price`

> **Wait — `current_price` is stored on `INGREDIENT` even though `PRICE_HISTORY` has all prices. Isn't that denormalized?**

Yes. This is **intentional performance denormalization**.

| Option | Query to get today's ingredient price |
|---|---|
| Fully normalized | `SELECT price FROM price_history WHERE ingredient_id = ? ORDER BY date DESC LIMIT 1` — expensive per ingredient |
| Denormalized `current_price` | `SELECT current_price FROM ingredient WHERE id = ?` — O(1) lookup |

Since recipe cost calculation queries `current_price` on *every ingredient in the recipe*, the join overhead of `MAX(date)` lookups would be significant. The tradeoff is accepted and documented.

**Rule of thumb**: Denormalize when the same derived value is read far more often than it changes, and the update path is controlled (i.e., you always update `current_price` when inserting a `PRICE_HISTORY` row).

---

### 2NF Analysis — PurchaseItem

`PURCHASE_ITEM` has a composite-like relationship: one purchase contains many items, each item references one ingredient.

**Violation (dependency on only part of the key):**

```sql
-- BAD: if purchase_item stored ingredient.name directly
CREATE TABLE purchase_item (
    id UUID PRIMARY KEY,
    purchase_id UUID,
    ingredient_id UUID,
    ingredient_name TEXT,   -- ← depends only on ingredient_id, not on this row's PK
    quantity FLOAT,
    price FLOAT
);
```

**Nastart solution — 2NF compliant:**

```
PURCHASE_ITEM
  id, purchase_id (FK), ingredient_id (FK), raw_text, quantity, price, status
```

`ingredient_name` is never stored here. You join to `INGREDIENT` when you need the name. Every column in `PURCHASE_ITEM` describes *this specific line item in this specific purchase*, not properties of the ingredient itself.

---

### Junction Table — RecipeIngredient

Recipes have a many-to-many relationship with ingredients. A recipe uses many ingredients; an ingredient appears in many recipes.

**Without a junction table (broken):**

```python
# Python list-on-object — impossible to query efficiently from SQL
recipe = {
    "id": "...",
    "name": "Bakpia",
    "ingredients": [
        {"id": "flour-id", "quantity": 500},
        {"id": "sugar-id", "quantity": 200},
    ]
}
```

**Nastart solution — proper junction table:**

```
RECIPE
  id, user_id, name, sell_price, total_cost, margin_percent, yield_quantity, yield_unit, status

RECIPE_INGREDIENT (junction)
  id, recipe_id (FK), ingredient_id (FK), quantity, cost

INGREDIENT
  id, user_id, category_id, name, unit, current_price, ...
```

The `RECIPE_INGREDIENT` table is the many-to-many bridge. It also carries *relationship data* — `quantity` (how much of this ingredient this recipe needs) and `cost` (the cached cost at the time of last calculation). These belong on the junction, not on either parent table.

---

## EF Core Implementation

### IEntityTypeConfiguration for Each Relationship

You already learned about `IEntityTypeConfiguration` in the advanced track. Here's how each normalized relationship gets configured:

#### CategoryConfiguration

```csharp
// Shared/Data/Configurations/CategoryConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nastart.Api.Shared.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Description)
            .HasMaxLength(300);

        // Unique: category names must be distinct (system-wide, not per-user)
        builder.HasIndex(c => c.Name).IsUnique();
    }
}
```

#### IngredientConfiguration — Category relationship

```csharp
// Shared/Data/Configurations/IngredientConfiguration.cs
public sealed class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.ToTable("ingredients");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.Unit)
            .IsRequired()
            .HasMaxLength(20);

        // Store money as decimal(18,4) — not float — to avoid floating-point errors
        builder.Property(i => i.CurrentPrice)
            .HasColumnType("decimal(18,4)");

        builder.Property(i => i.CurrentStock)
            .HasColumnType("decimal(18,4)");

        builder.Property(i => i.MinStock)
            .HasColumnType("decimal(18,4)");

        // ── Relationship: Ingredient belongs to one Category ──
        builder.HasOne(i => i.Category)           // navigation property
            .WithMany(c => c.Ingredients)          // reverse navigation
            .HasForeignKey(i => i.CategoryId)      // FK column
            .OnDelete(DeleteBehavior.Restrict);    // can't delete category with ingredients

        // ── Relationship: Ingredient has a history of prices ──
        builder.HasMany(i => i.PriceHistory)
            .WithOne(ph => ph.Ingredient)
            .HasForeignKey(ph => ph.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);     // deleting ingredient removes its history
    }
}
```

> **Why `decimal(18,4)` and not `float`?**
>
> `float` has rounding errors. `0.1 + 0.2 = 0.30000000000004` in floating-point. For money (IDR), you want *exact* values. Use `decimal` in C# and `decimal(18,4)` in the column type.

#### RecipeIngredientConfiguration — junction table

```csharp
// Shared/Data/Configurations/RecipeIngredientConfiguration.cs
public sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("recipe_ingredients");

        builder.HasKey(ri => ri.Id);
        builder.Property(ri => ri.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(ri => ri.Quantity)
            .HasColumnType("decimal(18,4)")
            .IsRequired();

        builder.Property(ri => ri.Cost)
            .HasColumnType("decimal(18,4)");

        // ── Relationship: RecipeIngredient belongs to one Recipe ──
        builder.HasOne(ri => ri.Recipe)
            .WithMany(r => r.Ingredients)
            .HasForeignKey(ri => ri.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);   // delete recipe → delete its ingredient lines

        // ── Relationship: RecipeIngredient references one Ingredient ──
        builder.HasOne(ri => ri.Ingredient)
            .WithMany(i => i.RecipeIngredients)
            .HasForeignKey(ri => ri.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);  // can't delete an ingredient used in a recipe
    }
}
```

#### PurchaseItemConfiguration — with explicit denorm note

```csharp
// Shared/Data/Configurations/PurchaseItemConfiguration.cs
public sealed class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        builder.ToTable("purchase_items");

        builder.HasKey(pi => pi.Id);

        // raw_text stores the original OCR string — useful for auditing/debugging
        // This is intentional denormalization: we keep the raw OCR source even after matching
        builder.Property(pi => pi.RawText).HasMaxLength(500);

        builder.Property(pi => pi.Quantity)
            .HasColumnType("decimal(18,4)");

        builder.Property(pi => pi.Price)
            .HasColumnType("decimal(18,4)");

        builder.Property(pi => pi.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("pending");

        // ── Relationship: PurchaseItem belongs to Purchase ──
        builder.HasOne(pi => pi.Purchase)
            .WithMany(p => p.Items)
            .HasForeignKey(pi => pi.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Relationship: PurchaseItem references Ingredient ──
        builder.HasOne(pi => pi.Ingredient)
            .WithMany()
            .HasForeignKey(pi => pi.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### Register Configurations in NastartDbContext

```csharp
// Shared/Data/NastartDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Auto-discover all IEntityTypeConfiguration<T> in this assembly
    // This scans for CategoryConfiguration, IngredientConfiguration, etc.
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(NastartDbContext).Assembly);
}
```

> This replaces manually calling `modelBuilder.ApplyConfiguration(new IngredientConfiguration())` for each entity. One line covers everything.

---

## Normalization Decisions Summary

| Design Decision | Normal Form | Reason |
|---|---|---|
| `CATEGORY` as separate table | 3NF | Avoid repeating category name on every ingredient |
| `PRICE_HISTORY` as separate table | 3NF | Avoid storing multiple price columns (price_jan, price_feb...) |
| `current_price` on `INGREDIENT` | *Intentional 3NF violation* | Read-heavy; avoids MAX(date) join on every recipe calculation |
| `RECIPE_INGREDIENT` junction table | 2NF / no BCNF violations | Many-to-many: recipe uses many ingredients, ingredient used in many recipes |
| `raw_text` on `PURCHASE_ITEM` | *Intentional denorm* | Audit trail of original OCR text for debugging |
| `cost` on `RECIPE_INGREDIENT` | *Intentional denorm* | Cached cost snapshot; avoids recalculating historical recipe costs |
| `total_cost`, `margin_percent` on `RECIPE` | *Intentional denorm* | Derived values cached for fast dashboard queries |

---

## ✅ Run It

After adding configurations, generate a migration to apply the schema:

```bash
dotnet ef migrations add NormalizedSchema
dotnet ef database update
```

Then verify in PostgreSQL:

```sql
-- Confirm foreign key constraints exist
SELECT
    tc.table_name,
    kcu.column_name,
    ccu.table_name AS foreign_table,
    ccu.column_name AS foreign_column
FROM information_schema.table_constraints tc
JOIN information_schema.key_column_usage kcu USING (constraint_name)
JOIN information_schema.constraint_column_usage ccu USING (constraint_name)
WHERE tc.constraint_type = 'FOREIGN KEY'
ORDER BY tc.table_name;
```

---

## Key Concepts

| Term | Definition |
|---|---|
| **1NF** | Each column holds one atomic value — no comma-separated lists |
| **2NF** | Every non-key column depends on the *full* primary key |
| **3NF** | No column depends on another *non-key* column (no transitive dependencies) |
| **Junction table** | A table that resolves a many-to-many relationship (e.g., `RECIPE_INGREDIENT`) |
| **Intentional denormalization** | Storing a derived/redundant value on purpose for read performance |
| **`HasColumnType("decimal(18,4)")`** | EF Core Fluent API to specify exact SQL column type for money values |
| **`OnDelete(DeleteBehavior.Cascade)`** | When parent is deleted, delete children automatically |
| **`OnDelete(DeleteBehavior.Restrict)`** | Block deletion of parent if children exist |
| **`ApplyConfigurationsFromAssembly()`** | Auto-registers all `IEntityTypeConfiguration<T>` implementations |

---

## Next

Lesson 15 → **Database Indexing** — now that the schema is normalized, which columns need indexes so queries don't do full-table scans?
