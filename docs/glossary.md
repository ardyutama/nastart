# Nastart Glossary — Ubiquitous Language

> **This is the single source of truth for naming.** Code, docs, and conversation must use these exact terms.

## Domain Terms

| Term | Definition | Code Example |
|------|------------|--------------|
| **User** | A registered business owner with optional Telegram link | `class User` |
| **Ingredient** | A trackable item used in recipes (tepung terigu, gula pasir, mentega) | `class Ingredient` |
| **Category** | A grouping for ingredients (Bahan Kering, Dairy, Bumbu) | `class Category` |
| **Purchase** | A single shopping transaction with receipt | `class Purchase` |
| **Purchase Item** | One line item on a receipt, matched or unmatched to an ingredient | `class PurchaseItem` |
| **Recipe** | A product formula with ingredients, quantities, and yield | `class Recipe` |
| **Recipe Item** | One ingredient entry in a recipe with quantity | `class RecipeItem` |
| **Shop** | A supplier/store where purchases are made | `class Shop` |
| **Receipt** | Photo proof of a purchase transaction | `string ReceiptImageUrl` |
| **Sale** | A recorded revenue event from selling a recipe product | `class Sale` |
| **Alert** | Notification triggered by business rules (price spike, low stock, margin danger) | `class Alert` |
| **Price History** | Historical price record for an ingredient over time | `class PriceHistory` |

## Financial Terms

| Term | Definition | Code Example |
|------|------------|--------------|
| **Cost** | Total ingredient expense to produce one batch | `decimal TotalCost` |
| **Cost Per Unit** | Total recipe cost divided by yield quantity | `decimal CostPerUnit` |
| **Sell Price** | The price charged to customers per unit (null if not yet set) | `decimal? SellPrice` |
| **Margin** | Percentage profit after deducting cost from sell price | `decimal MarginPercent` |
| **Min Margin** | User-configured minimum acceptable margin (default 20%) | `decimal MinMarginPercent` |
| **Price Spike** | When ingredient price increases >15% from last purchase | Triggers `PriceSpikeNotification` |
| **Inflation** | When ingredient price increases >10% over 30 days | Triggers inflation alert |

## Production Terms

| Term | Definition | Code Example |
|------|------------|--------------|
| **Yield** | Number of units a recipe produces per batch (e.g., 50 cookies) | `int YieldQuantity` |
| **Yield Unit** | The unit of measurement for yield (pcs, kg, loaf, box) | `string YieldUnit` |
| **Batch** | One production run of a recipe | Contextual — used in cost calculation |
| **Low Stock** | When ingredient quantity falls below minimum threshold | `bool IsLowStock` |
| **Unpriced Ingredient** | An ingredient with no known price yet (`CurrentPrice` is null) | `ingredient.HasPrice == false` |
| **Overhead** | Non-ingredient costs: packaging, gas, labor — *future feature* | Not yet modeled |

## Technical Terms

| Term | Definition | Code Example |
|------|------------|--------------|
| **Feature Slice** | A self-contained unit with Command/Query, Handler, Validator, and Endpoint | `Features/ScanReceipt/` |
| **Command** | A request that changes state (Create, Update, Delete) | `CreateIngredientCommand` |
| **Query** | A request that reads state (Get, List) | `GetRecipeCostQuery` |
| **Handler** | The class that processes a Command or Query | `CreateIngredientHandler` |
| **Notification** | MediatR event for side effects | `PriceChangedNotification` |
| **Result** | Success/Failure wrapper instead of exceptions | `Result<T>` |
| **HasPrice** | Computed property indicating an ingredient has a known price | `bool HasPrice => CurrentPrice.HasValue` |
| **HasCompleteCost** | Computed property indicating all recipe ingredients are priced | `bool HasCompleteCost => Items.All(i => i.Ingredient?.HasPrice == true)` |
| **First-Price Entry** | The first time a price is recorded for an unpriced ingredient (null → value) | Skips spike detection in `RecordPurchaseHandler` |
| **Scan Session** | Temporary state tracking a receipt OCR confirmation flow (expires in 10 min) | `class ScanSession` |
| **Fuzzy Match** | Approximate string matching to link OCR text to known ingredients | `FuzzyMatchingService` |
| **Onboarding** | First-time setup: business type selection + ingredient seeding | `/start` command flow |

## Naming Conventions

- **Entities**: PascalCase nouns — `Ingredient`, `Recipe`, `PurchaseItem`
- **Commands**: Verb + Noun + "Command" — `CreateIngredientCommand`
- **Queries**: "Get" + Noun + "Query" — `GetRecipeCostQuery`
- **Handlers**: Verb + Noun + "Handler" — `CreateIngredientHandler`
- **Notifications**: Event + "Notification" — `PriceChangedNotification`
- **Endpoints**: Noun + "Endpoints" — `IngredientsEndpoints`