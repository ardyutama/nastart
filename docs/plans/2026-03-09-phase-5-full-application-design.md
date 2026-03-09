# Phase 5 Design: Building the Full Nastart Application

**Date**: 2026-03-09  
**Track**: Beginner (.NET 10 for Python/JS developers)  
**Continues from**: Lesson 17 (Phase 4 — Database Mastery)

---

## Decisions

| Question | Decision |
|---|---|
| Feature scope | Ingredients (full) + Recipes + Purchases + Dashboard. OCR/Alerts deferred. |
| Schema approach | Extend existing project — migrate step by step from Phase 1–3 single-table design |
| Auth | Full JWT: register + login before any features. `ICurrentUser` service propagates `userId` |
| `user_id` in lessons | Extracted from JWT via `ICurrentUser` — never hardcoded or passed in headers |
| Purchases scope | Manual entry + `receipt_image_url` stored as stub. OCR in Phase 6. |

---

## Lesson Map

### Block 1 — Schema Migration + Auth (18–21)

| # | File | Builds | Key concepts |
|---|---|---|---|
| 18 | `18-schema-migration.md` | `User` + `Category` entities, migration, seed 4 default categories | `AddColumn` migrations, `HasData` seeding, `IEntityTypeConfiguration` |
| 19 | `19-registration.md` | `RegisterCommand`, BCrypt hash, unique email validation | `BCrypt.Net-Next`, `FluentValidation` email rules, `409 Conflict` on duplicate |
| 20 | `20-jwt-login.md` | `LoginCommand`, `JwtSecurityToken`, return `{ token, expiresAt }` | `System.IdentityModel.Tokens.Jwt`, symmetric key, JWT claims |
| 21 | `21-protect-endpoints.md` | `ICurrentUser` service, `AddAuthentication().AddJwtBearer()`, update `GetIngredientsQuery` to scope by user | `ClaimsPrincipal`, `[Authorize]` on route groups, `IHttpContextAccessor` |

### Block 2 — Ingredients Full Schema (22–23)

| # | File | Builds | Key concepts |
|---|---|---|---|
| 22 | `22-ingredients-full-schema.md` | Migration adds `user_id`, `category_id`, stock fields; update all handlers | `AddColumn` with nullable backfill, updating existing handlers, FK navigation |
| 23 | `23-price-history.md` | `UpdateIngredientPriceCommand`, insert `PriceHistory`, `PriceSpikeNotification` | `INotification` + `INotificationHandler`, two-table write, 15% spike threshold |

### Block 3 — Recipes (24–25)

| # | File | Builds | Key concepts |
|---|---|---|---|
| 24 | `24-recipes.md` | `CreateRecipeCommand` with embedded lines, `GetRecipesQuery`, `UpdateRecipeStatusCommand` | Nested command objects, junction table insert, status state machine |
| 25 | `25-recipe-costing.md` | `GetRecipeCostQuery` (live), `UpdateRecipeCostCommand` (cache write-back) | `Include().ThenInclude()`, denorm write-back, margin formula |

### Block 4 — Purchases (26–27)

| # | File | Builds | Key concepts |
|---|---|---|---|
| 26 | `26-record-purchase.md` | `RecordPurchaseCommand` — Purchase + PurchaseItems in one transaction, update stock/price, `receipt_image_url` stub | `IDbContextTransaction`, multi-table atomic write |
| 27 | `27-purchase-history.md` | `GetPurchaseHistoryQuery` (paginated), `GetPurchaseDetailQuery` | `PagedResult<T>`, `Skip/Take`, `Include` for detail |

### Block 5 — Dashboard (28)

| # | File | Builds | Key concepts |
|---|---|---|---|
| 28 | `28-dashboard.md` | `GetDashboardSummaryQuery` — 4 parallel DB queries | `Task.WhenAll()`, `GroupBy` aggregation, `DateOnly` month range |

---

## Architecture Conventions

- `ICurrentUser` injected into every handler needing `userId` — not read from `HttpContext` in handlers
- All commands/queries: `sealed record`
- Multi-table writes: `IDbContextTransaction`
- Response types: separate `sealed record` — never return EF entities from endpoints
- `[Authorize]` at route group level, not per-endpoint
- No refresh tokens (7-day JWT expiry)
- No `Result<T>` wrapper — use `TypedResults` directly
- No unit tests in Phase 5

---

## Folder Structure Added

```
Features/
├── Auth/
│   ├── User.cs
│   ├── Register.cs
│   ├── Login.cs
│   └── AuthEndpoints.cs
├── Ingredients/           ← extended
│   ├── UpdateIngredientPrice.cs
│   └── PriceHistory.cs
├── Recipes/
│   ├── Recipe.cs, RecipeIngredient.cs
│   ├── CreateRecipe.cs, GetRecipes.cs
│   ├── UpdateRecipeStatus.cs
│   ├── GetRecipeCost.cs, UpdateRecipeCost.cs
│   └── RecipesEndpoints.cs
├── Purchases/
│   ├── Purchase.cs, PurchaseItem.cs
│   ├── RecordPurchase.cs
│   ├── GetPurchaseHistory.cs, GetPurchaseDetail.cs
│   └── PurchasesEndpoints.cs
└── Dashboard/
    ├── GetDashboardSummary.cs
    └── DashboardEndpoints.cs
Shared/Services/
    └── CurrentUser.cs
```
