# Nastart Redesign: Domain-Enriched Consistency

> **Date**: 2026-02-19  
> **Status**: Approved  
> **Scope**: Fix all 11 lesson files + glossary + complete docs — no new lessons added

---

## Problem

The Nastart curriculum has 17 identified issues across 3 categories:

### A. Architecture & Code Consistency (compile-breaking)
1. **Entity model drift** — property names change between weeks (`StockQuantity` vs `CurrentStock`, `Total` vs `TotalAmount`, `SellPrice` vs `SellingPrice`)
2. **Triple/duplicate definitions** — `MarginBelowThresholdNotification` in 3 places, `RecalculateRecipeCostsHandler` in 2 places with different implementations
3. **Missing User entity** — bot commands reference `_db.Users` and `u.TelegramId` but no lesson defines User
4. **Bot architectural contradiction** — Weeks 8-10 put bot in `Nastart.Api`, Week 11 creates separate `Nastart.Bot` Dockerfile
5. **Result<T> pattern breaks** — different signatures in Week 2, 5, 9

### B. F&B Domain Gaps (from bakery business perspective)
6. **No yield/batch size** — recipes need "makes 50 cookies" to calculate cost per unit
7. **No unit conversion context** — bakers buy in kg, recipes use grams/tablespoons
8. **Status fields use magic strings** — should be enums per state diagrams
9. **No OCR traceability** — purchase items don't store raw OCR text
10. **OCR text cleaning bug** — replaces '0'→'O' and '1'→'I', corrupting prices

### C. Curriculum Structure
11. **WhatsApp vs Telegram confusion** — main docs feature WhatsApp, lessons use Telegram exclusively
12. **Missing frontend/CI/CD/auth lessons** — referenced in roadmap but don't exist
13. **Endpoint definitions conflict** between weeks

## Solution: Golden Entity Models

Establish canonical entity models in Week 2 as the "single source of truth". All subsequent weeks reference these models. Key changes:

### Standardized Property Names
| Entity | Old (inconsistent) | New (canonical) |
|--------|-------------------|-----------------|
| Ingredient | `StockQuantity` | `CurrentStock` |
| Purchase | `Total` | `TotalAmount` |
| Recipe | `SellingPrice` (W10) | `SellPrice` |
| Recipe | `IsActive` (W10) | `Status == RecipeStatus.Active` |

### New Entity: User
```csharp
public sealed class User
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Email { get; set; }
    public string? BusinessName { get; set; }
    public string BusinessType { get; set; } = "Bakery";
    public decimal MinMarginPercent { get; set; } = 20m;
    public long? TelegramId { get; set; }
    public string? TelegramUsername { get; set; }
    public bool IsOnboarded { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Domain Enrichments
- `Recipe.YieldQuantity` + `YieldUnit` — "makes 50 pcs"
- `Recipe.CostPerUnit` — computed cost per piece
- `PurchaseStatus` enum — replaces magic strings
- `RecipeStatus` enum — matches state diagram
- `PurchaseItem.RawText` — OCR traceability
- `Category.Description` — added to match EF config

### Notification Deduplication
- `MarginBelowThresholdNotification` — single definition in Week 4 `RecipeNotifications.cs`
- `RecalculateRecipeCostsHandler` — single definition in Week 4
- `PriceSpikeNotification` — single definition in Week 3 notification handlers

## Change Map

| File | Severity | Changes |
|------|----------|---------|
| glossary.md | Minor | Add ~9 new domain terms |
| nastart-complete-docs.md | Major | Fix DB schema, project structure, messaging strategy |
| Week 1 | Minor | Fix feature map to include User features |
| Week 2 | Major | Add User entity, standardize all entity models |
| Week 3 | Medium | Align Purchase model, fix notifications |
| Week 4 | Medium | Add yield/batch, fix duplicates, enum status |
| Week 5 | Medium | Align all EF configs to golden models |
| Week 6 | Minor | Fix OCR bug, align ScanReceipt interface |
| Week 7 | Minor | Align endpoints, note .NET 10 OpenAPI |
| Week 8 | Minor | Clarify custom models, add ngrok |
| Week 9 | Medium | Fix Result pattern, align signatures |
| Week 10 | Medium | Fix property refs, add User lookup |
| Week 11 | Minor | Clarify bot-in-api architecture |

## Approach
- Fix what exists — no new lesson files
- Enrich domain model where it naturally fits
- Maintain existing pedagogical structure and week progression
