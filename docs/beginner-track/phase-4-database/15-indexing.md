# Lesson 15: Database Indexing — Which Columns and Why

> **Phase**: 4 — Database Mastery
> **Track**: Beginner (.NET 10 for Python/JS developers)
> **Prerequisite**: Lesson 14 (Normalization) — schema structure is understood

---

## What You Will Learn

- What a database index is and how it speeds up queries
- Which Nastart columns need indexes based on actual query patterns
- When composite indexes beat single-column indexes
- How to define indexes with EF Core's `HasIndex()` Fluent API
- How to verify that PostgreSQL is actually using your index with `EXPLAIN ANALYZE`

---

## What Is a Database Index?

Without an index, PostgreSQL has to scan every row in a table to find matching rows — a **sequential scan** (like reading every page of a book to find a name).

An index is a separate sorted data structure. PostgreSQL can binary-search it to jump directly to the matching rows.

| Without index | With index |
|---|---|
| Scan all 50,000 ingredient rows | Jump to matching rows in O(log n) |
| SQL: `Seq Scan on ingredients` | SQL: `Index Scan using idx_ingredients_user_id` |

```
Book analogy:

No index:     open book at page 1, read every page to find "Flour"
Index:        open the index appendix, find "Flour → page 342", jump there
```

### The tradeoff

Indexes make **reads faster** but **writes slightly slower** (every INSERT/UPDATE must also update the index). Add indexes only for columns you actually query or filter on.

---

## Nastart Query Patterns → Index Strategy

Before adding indexes, ask: *what queries does this app actually run?*

### All queries are user-scoped

Every query in Nastart is scoped to a `user_id`. A user only sees their own ingredients, recipes, purchases. This means `user_id` appears in the `WHERE` clause of almost every query:

```sql
SELECT * FROM ingredients WHERE user_id = '...' AND ...
SELECT * FROM recipes WHERE user_id = '...' AND status = 'active'
SELECT * FROM purchases WHERE user_id = '...' ORDER BY purchase_date DESC
```

**Rule**: Any table with a `user_id` column needs an index on it.

---

## Index-by-Index Breakdown

### Index 1: `ingredients.user_id`

```sql
-- Every ingredient list query starts with this filter
SELECT * FROM ingredients WHERE user_id = $1
```

```csharp
// In IngredientConfiguration.cs
builder.HasIndex(i => i.UserId)
    .HasDatabaseName("idx_ingredients_user_id");
```

---

### Index 2: `ingredients.name` — for OCR fuzzy matching

OCR scans return text like `"TPNG SEGITIGA 1KG"`. The app fuzzy-matches this against ingredient names:

```sql
-- Trigram / LIKE query — benefits from a text search index
SELECT * FROM ingredients
WHERE user_id = $1
  AND name ILIKE '%tepung%'
```

For PostgreSQL full-text or ILIKE searches, add a PostgreSQL-specific `pg_trgm` GIN index:

```csharp
// In IngredientConfiguration.cs
builder.HasIndex(i => i.Name)
    .HasDatabaseName("idx_ingredients_name_trgm")
    .HasMethod("gin")      // tells EF Core to use GIN index type
    .HasOperators("gin_trgm_ops");  // enables trigram matching
```

> To use `gin_trgm_ops`, the `pg_trgm` extension must be enabled in PostgreSQL:
> ```sql
> CREATE EXTENSION IF NOT EXISTS pg_trgm;
> ```
> Add this to your initial migration's `Up()` method.

---

### Index 3: `purchases.user_id + purchase_date` — composite index

The purchase history query always filters by user *and* sorts by date:

```sql
SELECT * FROM purchases
WHERE user_id = $1
ORDER BY purchase_date DESC
LIMIT 10 OFFSET 0
```

A **composite index** on `(user_id, purchase_date)` covers both the filter and the sort in one index scan:

```csharp
// In PurchaseConfiguration.cs
builder.HasIndex(p => new { p.UserId, p.PurchaseDate })
    .HasDatabaseName("idx_purchases_user_date");
```

> **Column order matters in composite indexes.**
> Put the equality filter column first (`user_id`), the range/sort column second (`purchase_date`).
> The index `(user_id, purchase_date)` is useful for `WHERE user_id = X ORDER BY purchase_date`.
> The reverse `(purchase_date, user_id)` would NOT help this query.

---

### Index 4: `purchase_items.ingredient_id` — FK join

When calculating ingredient cost history, you join from `PURCHASE_ITEM` to find all purchases of a specific ingredient:

```sql
SELECT pi.price, p.purchase_date
FROM purchase_items pi
JOIN purchases p ON pi.purchase_id = p.id
WHERE pi.ingredient_id = $1
ORDER BY p.purchase_date DESC
```

```csharp
// In PurchaseItemConfiguration.cs
builder.HasIndex(pi => pi.IngredientId)
    .HasDatabaseName("idx_purchase_items_ingredient_id");
```

> **EF Core does NOT automatically create indexes on FK columns** in many cases. Always add them manually for columns you join on.

---

### Index 5: `price_history.ingredient_id + date` — price trend queries

Price trend analysis always filters by ingredient and sorts by date:

```sql
SELECT date, price FROM price_history
WHERE ingredient_id = $1
ORDER BY date DESC
LIMIT 30
```

```csharp
// In PriceHistoryConfiguration.cs (create this file)
public sealed class PriceHistoryConfiguration : IEntityTypeConfiguration<PriceHistory>
{
    public void Configure(EntityTypeBuilder<PriceHistory> builder)
    {
        builder.ToTable("price_history");

        builder.HasKey(ph => ph.Id);

        builder.Property(ph => ph.Price)
            .HasColumnType("decimal(18,4)");

        builder.HasOne(ph => ph.Ingredient)
            .WithMany(i => i.PriceHistory)
            .HasForeignKey(ph => ph.IngredientId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite: ingredient_id equality + date sort
        builder.HasIndex(ph => new { ph.IngredientId, ph.Date })
            .HasDatabaseName("idx_price_history_ingredient_date");
    }
}
```

---

### Index 6: `recipes.user_id + status` — active recipe list

The recipe listing query always filters by user and active status:

```sql
SELECT * FROM recipes
WHERE user_id = $1 AND status = 'active'
```

```csharp
// In RecipeConfiguration.cs
builder.HasIndex(r => new { r.UserId, r.Status })
    .HasDatabaseName("idx_recipes_user_status");
```

---

### Index 7: `alerts.user_id + read` — unread alert badge count

The dashboard shows how many unread alerts a user has — queried on every page load:

```sql
SELECT COUNT(*) FROM alerts
WHERE user_id = $1 AND read = false
```

```csharp
// In AlertConfiguration.cs
builder.HasIndex(a => new { a.UserId, a.Read })
    .HasDatabaseName("idx_alerts_user_read");
```

---

### Index 8: `sales.user_id + date` — profit summary by period

Monthly profit summaries aggregate sales within a date range:

```sql
SELECT SUM(profit) FROM sales
WHERE user_id = $1
  AND date >= '2025-01-01'
  AND date < '2025-02-01'
```

```csharp
// In SaleConfiguration.cs
builder.HasIndex(s => new { s.UserId, s.Date })
    .HasDatabaseName("idx_sales_user_date");
```

---

## Full Index Summary

```csharp
// All indexes in one view (spread across individual configuration files)

// ingredients
builder.HasIndex(i => i.UserId).HasDatabaseName("idx_ingredients_user_id");
builder.HasIndex(i => i.Name).HasDatabaseName("idx_ingredients_name_trgm")
       .HasMethod("gin").HasOperators("gin_trgm_ops");

// purchases
builder.HasIndex(p => new { p.UserId, p.PurchaseDate })
       .HasDatabaseName("idx_purchases_user_date");

// purchase_items
builder.HasIndex(pi => pi.IngredientId)
       .HasDatabaseName("idx_purchase_items_ingredient_id");

// price_history
builder.HasIndex(ph => new { ph.IngredientId, ph.Date })
       .HasDatabaseName("idx_price_history_ingredient_date");

// recipes
builder.HasIndex(r => new { r.UserId, r.Status })
       .HasDatabaseName("idx_recipes_user_status");

// alerts
builder.HasIndex(a => new { a.UserId, a.Read })
       .HasDatabaseName("idx_alerts_user_read");

// sales
builder.HasIndex(s => new { s.UserId, s.Date })
       .HasDatabaseName("idx_sales_user_date");
```

---

## Adding Indexes to Your Migration

After adding `HasIndex()` calls to your configurations, create a migration:

```bash
dotnet ef migrations add AddQueryIndexes
dotnet ef database update
```

The generated migration `Up()` will contain:

```csharp
migrationBuilder.CreateIndex(
    name: "idx_ingredients_user_id",
    table: "ingredients",
    column: "user_id");

migrationBuilder.CreateIndex(
    name: "idx_purchases_user_date",
    table: "purchases",
    columns: new[] { "user_id", "purchase_date" });

// etc.
```

---

## Verifying With EXPLAIN ANALYZE

After applying indexes, connect to PostgreSQL and run:

```sql
EXPLAIN ANALYZE
SELECT * FROM ingredients
WHERE user_id = 'your-user-uuid-here';
```

**Without index (bad):**
```
Seq Scan on ingredients  (cost=0.00..1200.00 rows=5000 width=120) (actual time=0.020..45.231 rows=47 loops=1)
  Filter: (user_id = '...')
  Rows Removed by Filter: 49953
Planning Time: 0.5 ms
Execution Time: 45.3 ms
```

**With index (good):**
```
Index Scan using idx_ingredients_user_id on ingredients  (cost=0.29..8.31 rows=47 width=120) (actual time=0.023..0.145 rows=47 loops=1)
  Index Cond: (user_id = '...')
Planning Time: 0.4 ms
Execution Time: 0.2 ms
```

The key indicators:
- `Index Scan` (or `Bitmap Index Scan`) = index is being used ✅
- `Seq Scan` = full table scan, index not used (check your query and index definition) ⚠️

### Run EXPLAIN from C# (EF Core)

```csharp
// For debugging in development only
var sql = db.Ingredients
    .Where(i => i.UserId == userId)
    .ToQueryString();  // prints the SQL EF Core will execute

Console.WriteLine(sql);
// Then manually run EXPLAIN ANALYZE <that SQL> in psql
```

---

## Index Design Rules (Summary)

| Scenario | Index type |
|---|---|
| `WHERE user_id = X` on its own | Single column: `HasIndex(x => x.UserId)` |
| `WHERE user_id = X ORDER BY date` | Composite: `HasIndex(x => new { x.UserId, x.Date })` |
| `WHERE user_id = X AND status = Y` | Composite: `HasIndex(x => new { x.UserId, x.Status })` |
| `WHERE name ILIKE '%text%'` | GIN trigram: `HasMethod("gin").HasOperators("gin_trgm_ops")` |
| FK join column (`ingredient_id`, `purchase_id`) | Single column on the FK side |
| `UNIQUE` constraint | `builder.HasIndex(x => x.Email).IsUnique()` |

---

## ✅ Run It

```bash
# 1. Add migrations after updating configurations
dotnet ef migrations add AddQueryIndexes

# 2. Apply to the database
dotnet ef database update

# 3. Check what indexes now exist in PostgreSQL
# (run in psql or DBeaver)
SELECT indexname, indexdef
FROM pg_indexes
WHERE tablename IN ('ingredients', 'purchases', 'recipes', 'alerts', 'sales', 'price_history', 'purchase_items')
ORDER BY tablename, indexname;
```

---

## Key Concepts

| Term | Definition |
|---|---|
| **Sequential scan** | PostgreSQL reads every row — O(n), slow for large tables |
| **Index scan** | PostgreSQL uses a B-tree/GIN index to jump to matching rows — O(log n) |
| **Composite index** | Index on multiple columns — covers both filter and sort in one structure |
| **GIN index** | PostgreSQL index type suited for full-text and trigram (ILIKE) searches |
| **`HasIndex()`** | EF Core Fluent API to declare an index |
| **`HasMethod("gin")`** | Specifies the PostgreSQL index method in EF Core |
| **Column order** | In composite indexes, equality filter columns come first, range/sort columns last |
| **`EXPLAIN ANALYZE`** | PostgreSQL command to show the query execution plan and whether indexes are used |
| **`ToQueryString()`** | EF Core method to debug-print the SQL a LINQ query will generate |

---

## Next

Lesson 16 → **Efficient LINQ/EF Core Queries** — indexes make individual rows fast to find. Now learn how to write queries that don't accidentally load 10,000 rows when you only need 5.
