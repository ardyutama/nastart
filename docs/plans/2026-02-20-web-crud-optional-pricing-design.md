# Web CRUD & Optional Pricing Design

> **Date**: 2026-02-20
> **Status**: Approved
> **Scope**: New web-based CRUD for ingredients/recipes + optional pricing on ingredients and recipes

---

## Problem

Nastart currently only supports adding ingredients through receipt OCR via the Telegram bot. This creates friction:

1. **Pantry tracking** — Bakers have ingredients at home but don't remember prices. They can't list what they have.
2. **Recipe planning** — Bakers want to design recipes first, figure out costs later.
3. **Gradual data entry** — New users want to set up their ingredient list quickly without hunting for every price.

There is no web-based management interface — all data entry flows through the bot.

## Solution: Optional Pricing + Web CRUD

Two changes working together:

1. **Make `CurrentPrice` nullable** on `Ingredient` — `null` means "not yet priced", `0` means "genuinely free"
2. **Add full CRUD feature slices** for ingredients and recipes accessible from the web dashboard

### Design Decisions

| Decision | Choice | Reasoning |
|----------|--------|-----------|
| How to represent "no price" | `decimal? CurrentPrice` (nullable) | C# convention: null = no value. Distinct from 0 (free). |
| Recipe costing with missing prices | Block calculation, return list of unpriced ingredients | Keeps financial data trustworthy — no partial/wrong costs |
| Recipe SellPrice in Draft | Optional (`decimal?`) | Drafts are for planning; pricing comes at Costing stage |
| Delete ingredient in use | Block with 409 Conflict | Protect recipe integrity |
| First price entry (null → value) | Skip spike detection | It's not a "change", it's a first entry |
| Web scope | Full CRUD (create, read, update, delete) | Users need complete management, not just creation |

---

## Entity Model Changes

### Ingredient — Nullable Price

```csharp
public class Ingredient
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Name { get; set; }
    public required string Unit { get; set; }
    public decimal? CurrentPrice { get; set; }    // CHANGED: was decimal (required)
    public string Currency { get; set; } = "IDR";
    public decimal CurrentStock { get; set; }
    public decimal MinStock { get; set; }
    public Guid? CategoryId { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Computed properties
    public bool HasPrice => CurrentPrice.HasValue;
    public bool IsLowStock => CurrentStock <= MinStock;

    // Navigation properties
    public User User { get; set; } = null!;
    public Category? Category { get; set; }
}
```

### Recipe — Optional SellPrice in Draft + Completeness Check

```csharp
public class Recipe
{
    // ... existing fields ...
    public decimal? SellPrice { get; set; }       // CHANGED: nullable, required only for Costing+

    // New computed property
    public bool HasCompleteCost => Items.All(i => i.Ingredient?.HasPrice == true);
}
```

### CreateIngredient Command — Price Optional

```csharp
public sealed record CreateIngredientCommand(
    Guid UserId,
    string Name,
    string Unit,
    decimal? InitialPrice,    // CHANGED: was decimal (required)
    decimal MinStock,
    Guid? CategoryId
) : IRequest<Result<IngredientResponse>>;
```

Validator: `RuleFor(x => x.InitialPrice).GreaterThanOrEqualTo(0).When(x => x.InitialPrice.HasValue);`

### CreateRecipe Command — SellPrice Optional in Draft

```csharp
public sealed record CreateRecipeCommand(
    Guid UserId,
    string Name,
    string? Description,
    string? Category,
    decimal? SellPrice,       // CHANGED: optional for Draft status
    int YieldQuantity,
    string YieldUnit
) : IRequest<Result<RecipeResponse>>;
```

---

## New Feature Slices

### Ingredients Web CRUD

| Feature Slice | Method | Route | Notes |
|---------------|--------|-------|-------|
| `CreateIngredient` | POST | `/api/ingredients` | Existing — make price optional |
| `GetIngredients` | GET | `/api/ingredients` | Existing — add `?hasPrice=true/false` filter |
| `GetIngredient` | GET | `/api/ingredients/{id}` | Existing |
| `UpdateIngredient` | PUT | `/api/ingredients/{id}` | **New** — update name, unit, price, stock, category |
| `DeleteIngredient` | DELETE | `/api/ingredients/{id}` | **New** — block if used in active recipes |

### Recipes Web CRUD

| Feature Slice | Method | Route | Notes |
|---------------|--------|-------|-------|
| `CreateRecipe` | POST | `/api/recipes` | Existing — SellPrice optional in Draft |
| `GetRecipes` | GET | `/api/recipes` | **New** — list with cost summary, filter by status |
| `GetRecipe` | GET | `/api/recipes/{id}` | **New** — detail with items and cost breakdown |
| `UpdateRecipe` | PUT | `/api/recipes/{id}` | **New** — update name, description, sell price, yield |
| `DeleteRecipe` | DELETE | `/api/recipes/{id}` | **New** — only Draft/Inactive can be deleted |
| `AddIngredientToRecipe` | POST | `/api/recipes/{id}/items` | Existing |
| `RemoveIngredientFromRecipe` | DELETE | `/api/recipes/{id}/items/{itemId}` | Existing |
| `UpdateRecipeItem` | PUT | `/api/recipes/{id}/items/{itemId}` | **New** — change quantity |

---

## Costing Block Logic

`GetRecipeCost` handler returns two shapes:

**When all ingredients have prices (complete):**
```json
{
  "isComplete": true,
  "totalCost": 45000,
  "costPerUnit": 1875,
  "marginPercent": 25.0,
  "items": [
    { "ingredientName": "Flour", "quantity": 500, "unitPrice": 12000, "cost": 6000 }
  ]
}
```

**When some ingredients are missing prices (incomplete):**
```json
{
  "isComplete": false,
  "missingPrices": ["Flour", "Vanilla Extract"],
  "message": "2 ingredients need prices before cost can be calculated"
}
```

---

## Impact on Existing Features

| Existing Feature | Change Needed |
|------------------|---------------|
| `RecordPurchase` | None — purchase items always have prices from receipts |
| `UpdateIngredientPrice` | Handle first-price scenario (null → value): skip spike detection |
| `PriceChangedNotification` | Guard: only fire when old price was not null |
| `GetRecipeCost` | Add completeness check before calculation |
| `ScanReceipt` / OCR | None — OCR always provides prices |
| `GetLowStockIngredients` | None — stock is independent of price |

### First-Price vs Price-Change Logic

```
IF oldPrice IS NULL AND newPrice IS NOT NULL:
    → This is a "first price entry"
    → Update ingredient, NO spike detection, NO PriceChangedNotification
    → DO recalculate recipe costs (now that we have a price)
    
IF oldPrice IS NOT NULL AND newPrice IS NOT NULL:
    → This is a "price change" (existing behavior)
    → Fire PriceChangedNotification
    → Check for spike (>15% change)
    → Recalculate recipe costs
```

---

## Error Handling

| Scenario | HTTP Status | Message |
|----------|-------------|---------|
| Delete ingredient used in active recipe | 409 Conflict | "Ingredient is used in 3 active recipes: [names]. Deactivate or remove from recipes first." |
| Delete recipe in Active status | 400 Bad Request | "Active recipes must be deactivated before deletion." |
| Move recipe Draft → Costing without SellPrice | 400 Bad Request | "SellPrice is required to move to Costing status." |
| Calculate cost with unpriced ingredients | 200 OK | Return `isComplete: false` + `missingPrices` list |
| Update price to negative | 400 Bad Request | "Price must be zero or greater." |
| Update non-existent ingredient | 404 Not Found | "Ingredient not found." |
| Update ingredient owned by another user | 403 Forbidden | "You don't have access to this ingredient." |

---

## Data Flow — Manual Web Entry

```
Baker opens web dashboard
  → POST /api/ingredients { name: "Flour", unit: "kg" }
    → price is null, ingredient saved as "unpriced"
    → Dashboard shows ingredient with "No price" badge

Baker adds more ingredients (some with prices, some without)
  → Ingredient list shows mix of priced/unpriced

Baker creates a recipe
  → POST /api/recipes { name: "Nastar Cookies", yieldQuantity: 50, yieldUnit: "pieces" }
    → Recipe saved as Draft (no SellPrice needed yet)

Baker adds ingredients to recipe
  → POST /api/recipes/{id}/items { ingredientId: ..., quantity: 500 }

Baker checks recipe cost
  → GET /api/recipes/{id}/cost
    → Returns: { isComplete: false, missingPrices: ["Flour"] }

Baker updates flour price (found the receipt)
  → PUT /api/ingredients/{flourId} { currentPrice: 12000 }
    → First-price entry: no spike alert, recipe costs recalculated

Baker checks recipe cost again
  → GET /api/recipes/{id}/cost
    → Returns: { isComplete: true, totalCost: 45000, costPerUnit: 900 }

Baker sets sell price and publishes
  → PUT /api/recipes/{id} { sellPrice: 2000 }
  → Recipe moves Draft → Costing → Ready → Active
```

---

## Testing Strategy

### Unit Tests
- `CreateIngredient` with null price succeeds
- `CreateIngredient` with negative price fails validation
- `CreateIngredient` with `price = 0` succeeds (free ingredient)
- `GetRecipeCost` returns incomplete when ingredients lack prices
- `GetRecipeCost` returns complete when all prices present
- `UpdateIngredientPrice` from null → value skips spike detection
- `UpdateIngredientPrice` from value → value fires PriceChangedNotification
- `DeleteIngredient` blocked when used in active recipe

### Validator Tests
- Price: null passes, negative fails, zero passes, positive passes
- SellPrice: null passes for Draft, null fails for Costing transition

### Integration Tests
- Full lifecycle: create unpriced → add to recipe → costing blocked → add price → costing works
- Delete protection: create ingredient → add to active recipe → delete blocked

---

## Lesson Impact

This design affects the following lesson files:

| Lesson | Changes |
|--------|---------|
| Week 2 (Domain Foundation) | Ingredient entity: `CurrentPrice` → `decimal?`, add `HasPrice`, update `CreateIngredient` command/validator |
| Week 4 (Recipe Domain) | Recipe entity: `SellPrice` → `decimal?`, add `HasCompleteCost`, update `CreateRecipe` command |
| Week 4 (Recipe Domain) | `GetRecipeCost` handler: add completeness check |
| Week 3 (Finance Domain) | `UpdateIngredientPrice`: add first-price guard for notifications |
| Week 5 (Database) | EF Core config: update Ingredient price column to nullable |
| Week 7 (API Polish) | Document new endpoints in OpenAPI |
| Complete Docs | Update DB schema, feature list, project structure |
| Glossary | Add terms: HasPrice, HasCompleteCost, First-Price Entry, Unpriced Ingredient |

### New Feature Slice Files

```
Features/
├── Ingredients/
│   ├── UpdateIngredient.cs       # NEW — full update (name, unit, price, stock, category)
│   └── DeleteIngredient.cs       # NEW — soft delete with active recipe guard
│
└── Recipes/
    ├── GetRecipes.cs             # NEW — list with filters
    ├── GetRecipe.cs              # NEW — detail with items
    ├── UpdateRecipe.cs           # NEW — update fields
    ├── DeleteRecipe.cs           # NEW — with status guard
    └── UpdateRecipeItem.cs       # NEW — change quantity
```
