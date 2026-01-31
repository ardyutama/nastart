# Week 3: Finance Context Domain 💰

> **Goal**: Build the Finance bounded context — Purchase aggregate with PurchaseItem entities, PriceHistory for tracking, and PriceAnalysisService for detecting price spikes.

---

## Table of Contents
1. [Day 1: Strongly-Typed IDs](#day-1-strongly-typed-ids)
2. [Day 2: Purchase Aggregate Root](#day-2-purchase-aggregate-root)
3. [Day 3: PurchaseItem Entity](#day-3-purchaseitem-entity)
4. [Day 4: PriceHistory Aggregate](#day-4-pricehistory-aggregate)
5. [Day 5: Domain Service — PriceAnalysisService](#day-5-domain-service--priceanalysisservice)
6. [Day 6: Domain Events — PriceSpikeDetectedEvent](#day-6-domain-events--pricespikedetectedevent)
7. [Day 7: Unit Testing Finance Domain](#day-7-unit-testing-finance-domain)
8. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1: Strongly-Typed IDs

## 🧒 Explain Like I'm 5

Imagine you have a toy box with numbers on each toy. Your teddy bear is #5 and your car is also #5. If you just say "Give me toy #5" — which one do you mean?

**Strongly-Typed IDs** are like labels that say:
- "Teddy Bear #5" 🧸
- "Car #5" 🚗

Now we can never mix them up!

## 🔧 Engineer Language

**Strongly-Typed IDs** (also called "Typed IDs" or "Wrapper Types") wrap primitive types (like `int` or `Guid`) in domain-specific types. This prevents accidentally passing an `IngredientId` where a `PurchaseId` is expected.

> 📖 **Microsoft Docs**: *"In domain-driven design (DDD), 'guarded keys' can improve the type safety of key properties. This is achieved by wrapping the key type in another type which is specific to the use of the key."*
>
> — [What's New in EF Core 7.0 - Value Generation for DDD](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-7.0/whatsnew#value-generation-for-ddd-guarded-types)

### Why Use Strongly-Typed IDs?

| Problem | Without Typed IDs | With Typed IDs |
|---------|-------------------|----------------|
| Parameter mix-up | `ProcessPurchase(int purchaseId, int ingredientId)` — easy to swap! | `ProcessPurchase(PurchaseId id, IngredientId ingId)` — compiler catches mistakes |
| Code readability | `int id` — What kind of ID? | `PurchaseId id` — Clear intent |
| Refactoring | Change `int` to `Guid`? Massive search/replace | Change internal type once |

### Implementation Using `record struct` (.NET 10 Best Practice):

Create `src/Nastart.Domain/Finance/ValueObjects/FinanceIds.cs`:

```csharp
namespace Nastart.Domain.Finance.ValueObjects;

/// <summary>
/// Strongly-typed ID for Purchase aggregate.
/// Using record struct for value semantics and zero allocation.
/// </summary>
/// <remarks>
/// See: https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-7.0/whatsnew#value-generation-for-ddd-guarded-types
/// </remarks>
public readonly record struct PurchaseId(Guid Value)
{
    /// <summary>Creates a new unique PurchaseId.</summary>
    public static PurchaseId New() => new(Guid.NewGuid());
    
    /// <summary>Represents an empty/unset PurchaseId.</summary>
    public static PurchaseId Empty => new(Guid.Empty);
    
    /// <summary>Checks if this ID has been set.</summary>
    public bool IsEmpty => Value == Guid.Empty;

    public override string ToString() => Value.ToString();

    /// <summary>Implicit conversion from Guid for convenience.</summary>
    public static implicit operator Guid(PurchaseId id) => id.Value;
    
    /// <summary>Explicit conversion to PurchaseId requires intention.</summary>
    public static explicit operator PurchaseId(Guid guid) => new(guid);
}

/// <summary>
/// Strongly-typed ID for PurchaseItem entity.
/// </summary>
public readonly record struct PurchaseItemId(Guid Value)
{
    public static PurchaseItemId New() => new(Guid.NewGuid());
    public static PurchaseItemId Empty => new(Guid.Empty);
    public bool IsEmpty => Value == Guid.Empty;
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed ID for Sale entity.
/// </summary>
public readonly record struct SaleId(Guid Value)
{
    public static SaleId New() => new(Guid.NewGuid());
    public static SaleId Empty => new(Guid.Empty);
    public bool IsEmpty => Value == Guid.Empty;
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed ID for PriceHistory record.
/// </summary>
public readonly record struct PriceHistoryId(Guid Value)
{
    public static PriceHistoryId New() => new(Guid.NewGuid());
    public static PriceHistoryId Empty => new(Guid.Empty);
    public bool IsEmpty => Value == Guid.Empty;
    public override string ToString() => Value.ToString();
}
```

### EF Core Value Converter (for Infrastructure layer later):

```csharp
// This goes in Nastart.Infrastructure — showing for reference
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public class PurchaseIdConverter : ValueConverter<PurchaseId, Guid>
{
    public PurchaseIdConverter() 
        : base(
            id => id.Value,           // To database
            guid => new PurchaseId(guid))  // From database
    { }
}
```

### Modern .NET 10 Features Used:

| Feature | Example | Benefit |
|---------|---------|---------|
| `record struct` | `public readonly record struct PurchaseId` | Value semantics, stack-allocated |
| `readonly` modifier | `readonly record struct` | Immutable, thread-safe |
| Primary constructor | `PurchaseId(Guid Value)` | Concise syntax |
| Implicit operators | `public static implicit operator Guid` | Seamless interop |

### Folder Structure After Day 1:

```
src/Nastart.Domain/
├── Common/                        # From Week 2
│   ├── Entity.cs
│   ├── AggregateRoot.cs
│   └── ValueObject.cs
├── Finance/
│   ├── Aggregates/               # Coming Day 2-4
│   ├── Entities/                 # Coming Day 3
│   ├── ValueObjects/
│   │   └── FinanceIds.cs         # ← Created today
│   ├── Events/                   # Coming Day 6
│   └── Services/                 # Coming Day 5
└── Inventory/                    # From Week 2
```

### Your Task (Day 1):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Domain

# Create Finance folder structure
mkdir Finance\Aggregates
mkdir Finance\Entities  
mkdir Finance\ValueObjects
mkdir Finance\Events
mkdir Finance\Services

# Create the FinanceIds.cs file
# (Use VS Code or your editor)
```

---

# Day 2: Purchase Aggregate Root

## 🧒 Explain Like I'm 5

When you go shopping with mom, you get a **receipt**. The receipt shows:
- 📅 When you shopped (date)
- 🏪 Where you shopped (store name)
- 📝 What you bought (list of items)
- 💰 How much everything cost (total)

A **Purchase** is like that receipt in our app!

## 🔧 Engineer Language

The **Purchase** aggregate is the root of a consistency boundary that includes:
- The purchase itself (date, shop, receipt image)
- A collection of **PurchaseItem** entities (what was bought)
- Business rules for adding items and calculating totals

> 📖 **Microsoft Docs**: *"An aggregate is composed of at least one entity: the aggregate root. Additionally, it can have multiple child entities and value objects, with all entities and objects working together to implement required behavior and transactions."*
>
> — [Design a microservice domain model - The Aggregate Root pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model#the-aggregate-root-or-root-entity-pattern)

### Key DDD Rules for Aggregates:

| Rule | Application to Purchase |
|------|-------------------------|
| **Single entry point** | All changes go through `Purchase` methods |
| **Encapsulate child entities** | `PurchaseItems` collection is private, exposed as `IReadOnlyCollection` |
| **Maintain invariants** | Total is always recalculated when items change |
| **Raise domain events** | Raises `PurchaseRecordedEvent` when completed |

### Purchase Aggregate Implementation:

Create `src/Nastart.Domain/Finance/Aggregates/Purchase.cs`:

```csharp
using Nastart.Domain.Common;
using Nastart.Domain.Finance.Entities;
using Nastart.Domain.Finance.ValueObjects;
using Nastart.Domain.Finance.Events;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Domain.Finance.Aggregates;

/// <summary>
/// Purchase aggregate root — represents a shopping transaction with receipt.
/// All modifications to purchase items must go through this aggregate root.
/// </summary>
/// <remarks>
/// Follows eShopOnContainers pattern for aggregate design.
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/net-core-microservice-domain-model
/// </remarks>
public class Purchase : AggregateRoot
{
    // Private backing field for child entities
    private readonly List<PurchaseItem> _purchaseItems;

    // Private setters ensure encapsulation
    public PurchaseId PurchaseId { get; private set; }
    public DateOnly PurchaseDate { get; private set; }
    public Guid ShopId { get; private set; }  // FK to Shop aggregate
    public Guid UserId { get; private set; }  // FK to User
    public string? ReceiptImageUrl { get; private set; }
    public Money Total { get; private set; }
    public PurchaseStatus Status { get; private set; }

    /// <summary>
    /// Exposes items as read-only collection.
    /// External code cannot modify the collection directly.
    /// </summary>
    public IReadOnlyCollection<PurchaseItem> PurchaseItems => _purchaseItems.AsReadOnly();

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private Purchase()
    {
        _purchaseItems = [];
        PurchaseId = PurchaseId.Empty;
        Total = Money.Zero;
        Status = PurchaseStatus.Draft;
    }

    /// <summary>
    /// Creates a new Purchase aggregate.
    /// </summary>
    /// <param name="userId">The user who made the purchase.</param>
    /// <param name="shopId">The shop where purchase was made.</param>
    /// <param name="purchaseDate">Date of purchase.</param>
    /// <param name="receiptImageUrl">Optional URL to receipt image.</param>
    public Purchase(
        Guid userId,
        Guid shopId,
        DateOnly purchaseDate,
        string? receiptImageUrl = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));
        
        if (shopId == Guid.Empty)
            throw new ArgumentException("Shop ID is required.", nameof(shopId));

        PurchaseId = PurchaseId.New();
        UserId = userId;
        ShopId = shopId;
        PurchaseDate = purchaseDate;
        ReceiptImageUrl = receiptImageUrl;
        Total = Money.Zero;
        Status = PurchaseStatus.Draft;
        _purchaseItems = [];
    }

    /// <summary>
    /// Adds a new item to this purchase.
    /// Recalculates total automatically.
    /// </summary>
    /// <remarks>
    /// This is the ONLY way to add items — enforces business rules.
    /// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/net-core-microservice-domain-model#encapsulate-data-in-the-domain-entities
    /// </remarks>
    public PurchaseItem AddItem(
        IngredientId ingredientId,
        string ingredientName,
        Quantity quantity,
        Money unitPrice)
    {
        if (Status == PurchaseStatus.Confirmed)
            throw new InvalidOperationException("Cannot modify a confirmed purchase.");

        // Check for duplicate ingredient in this purchase
        var existingItem = _purchaseItems.FirstOrDefault(
            i => i.IngredientId == ingredientId);

        if (existingItem is not null)
        {
            // Update existing item instead of adding duplicate
            existingItem.UpdateQuantity(
                existingItem.Quantity.Add(quantity), 
                unitPrice);
            RecalculateTotal();
            return existingItem;
        }

        // Create new item
        var item = new PurchaseItem(
            purchaseId: PurchaseId,
            ingredientId: ingredientId,
            ingredientName: ingredientName,
            quantity: quantity,
            unitPrice: unitPrice);

        _purchaseItems.Add(item);
        RecalculateTotal();

        return item;
    }

    /// <summary>
    /// Removes an item from this purchase by its ID.
    /// </summary>
    public void RemoveItem(PurchaseItemId itemId)
    {
        if (Status == PurchaseStatus.Confirmed)
            throw new InvalidOperationException("Cannot modify a confirmed purchase.");

        var item = _purchaseItems.FirstOrDefault(i => i.ItemId == itemId);
        
        if (item is null)
            throw new InvalidOperationException($"Item {itemId} not found in this purchase.");

        _purchaseItems.Remove(item);
        RecalculateTotal();
    }

    /// <summary>
    /// Updates the quantity and price of an existing item.
    /// </summary>
    public void UpdateItem(PurchaseItemId itemId, Quantity newQuantity, Money newUnitPrice)
    {
        if (Status == PurchaseStatus.Confirmed)
            throw new InvalidOperationException("Cannot modify a confirmed purchase.");

        var item = _purchaseItems.FirstOrDefault(i => i.ItemId == itemId)
            ?? throw new InvalidOperationException($"Item {itemId} not found in this purchase.");

        item.UpdateQuantity(newQuantity, newUnitPrice);
        RecalculateTotal();
    }

    /// <summary>
    /// Confirms the purchase, making it immutable.
    /// Raises domain events for each price change detected.
    /// </summary>
    public void Confirm()
    {
        if (_purchaseItems.Count == 0)
            throw new InvalidOperationException("Cannot confirm an empty purchase.");

        if (Status == PurchaseStatus.Confirmed)
            throw new InvalidOperationException("Purchase is already confirmed.");

        Status = PurchaseStatus.Confirmed;

        // Raise domain event
        AddDomainEvent(new PurchaseRecordedEvent(
            PurchaseId,
            UserId,
            ShopId,
            PurchaseDate,
            Total,
            _purchaseItems.Select(i => new PurchaseRecordedEvent.ItemInfo(
                i.IngredientId,
                i.IngredientName,
                i.UnitPrice)).ToList()));
    }

    /// <summary>
    /// Attaches or updates the receipt image URL.
    /// </summary>
    public void AttachReceiptImage(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new ArgumentException("Image URL cannot be empty.", nameof(imageUrl));

        ReceiptImageUrl = imageUrl;
    }

    /// <summary>
    /// Recalculates the total from all items.
    /// Called automatically when items change.
    /// </summary>
    private void RecalculateTotal()
    {
        Total = _purchaseItems
            .Select(item => item.LineTotal)
            .Aggregate(Money.Zero, (acc, lineTotal) => acc.Add(lineTotal));
    }
}

/// <summary>
/// Purchase status enumeration.
/// </summary>
public enum PurchaseStatus
{
    /// <summary>Purchase is being created, can be modified.</summary>
    Draft = 0,
    
    /// <summary>Purchase is confirmed and immutable.</summary>
    Confirmed = 1,
    
    /// <summary>Purchase was cancelled.</summary>
    Cancelled = 2
}
```

### Key Design Decisions:

| Decision | Rationale |
|----------|-----------|
| `_purchaseItems` is private `List<>` | Only aggregate root can modify |
| `PurchaseItems` returns `IReadOnlyCollection` | Prevents external modification |
| `AddItem()` handles duplicates | Business logic stays in domain |
| `RecalculateTotal()` is private | Called automatically, ensures consistency |
| `Confirm()` raises domain event | Triggers price tracking workflow |

### Your Task (Day 2):

1. Create the `Purchase.cs` file
2. Note we need `AggregateRoot` base class from Week 2
3. Note we need value objects (`Money`, `Quantity`, `IngredientId`) from Week 2
4. Build to check for errors: `dotnet build`

---

# Day 3: PurchaseItem Entity

## 🧒 Explain Like I'm 5

When you look at a receipt, each LINE is different:
- Line 1: Flour, 1 kg, Rp 15,000
- Line 2: Sugar, 500g, Rp 8,000
- Line 3: Eggs, 10 pcs, Rp 25,000

Each line is a **PurchaseItem**! It tells us:
- WHAT you bought
- HOW MUCH you bought
- HOW MUCH it cost

## 🔧 Engineer Language

A **PurchaseItem** is a **child entity** within the Purchase aggregate. It has:
- Its own identity (`PurchaseItemId`)
- Belongs to exactly one Purchase
- Cannot exist without a parent Purchase

> 📖 **Microsoft Docs**: *"An order item will usually be an entity. But it will be a child entity within the order aggregate, which will also contain the order entity as its root entity."*
>
> — [Design a microservice domain model - The Aggregate pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model#the-aggregate-pattern)

### PurchaseItem Entity Implementation:

Create `src/Nastart.Domain/Finance/Entities/PurchaseItem.cs`:

```csharp
using Nastart.Domain.Common;
using Nastart.Domain.Finance.ValueObjects;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Domain.Finance.Entities;

/// <summary>
/// Child entity within the Purchase aggregate.
/// Represents a single line item on a receipt.
/// </summary>
/// <remarks>
/// Based on OrderItem pattern from eShopOnContainers.
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/net-core-microservice-domain-model
/// </remarks>
public class PurchaseItem : Entity
{
    public PurchaseItemId ItemId { get; private set; }
    public PurchaseId PurchaseId { get; private set; }  // FK to parent aggregate
    public IngredientId IngredientId { get; private set; }
    public string IngredientName { get; private set; }
    public Quantity Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money LineTotal { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private PurchaseItem()
    {
        ItemId = PurchaseItemId.Empty;
        PurchaseId = PurchaseId.Empty;
        IngredientId = IngredientId.Empty;
        IngredientName = string.Empty;
        Quantity = Quantity.Zero("pcs");
        UnitPrice = Money.Zero;
        LineTotal = Money.Zero;
    }

    /// <summary>
    /// Creates a new PurchaseItem.
    /// Should only be called by the parent Purchase aggregate.
    /// </summary>
    internal PurchaseItem(
        PurchaseId purchaseId,
        IngredientId ingredientId,
        string ingredientName,
        Quantity quantity,
        Money unitPrice)
    {
        if (ingredientId.IsEmpty)
            throw new ArgumentException("Ingredient ID is required.", nameof(ingredientId));
        
        if (string.IsNullOrWhiteSpace(ingredientName))
            throw new ArgumentException("Ingredient name is required.", nameof(ingredientName));
        
        if (quantity.Value <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        
        if (unitPrice.Amount < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));

        ItemId = PurchaseItemId.New();
        PurchaseId = purchaseId;
        IngredientId = ingredientId;
        IngredientName = ingredientName;
        Quantity = quantity;
        UnitPrice = unitPrice;
        
        CalculateLineTotal();
    }

    /// <summary>
    /// Updates the quantity and recalculates line total.
    /// Internal — can only be called by parent aggregate.
    /// </summary>
    internal void UpdateQuantity(Quantity newQuantity, Money newUnitPrice)
    {
        if (newQuantity.Value <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(newQuantity));
        
        if (newUnitPrice.Amount < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(newUnitPrice));

        Quantity = newQuantity;
        UnitPrice = newUnitPrice;
        CalculateLineTotal();
    }

    /// <summary>
    /// Calculates the line total (quantity × unit price).
    /// </summary>
    private void CalculateLineTotal()
    {
        // LineTotal = Quantity.Value × UnitPrice.Amount
        var totalAmount = Quantity.Value * UnitPrice.Amount;
        LineTotal = new Money(totalAmount, UnitPrice.Currency);
    }
}
```

### Key Design Decisions:

| Decision | Rationale |
|----------|-----------|
| Constructor is `internal` | Only `Purchase` aggregate can create items |
| `UpdateQuantity` is `internal` | Only parent can trigger updates |
| `CalculateLineTotal` is private | Called automatically, ensures consistency |
| Stores `IngredientName` | Denormalized for display (ingredient name might change) |

### Your Task (Day 3):

1. Create the `PurchaseItem.cs` file
2. Make sure you have `IngredientId` from Week 2 (in Inventory/ValueObjects)
3. Build to check: `dotnet build`

---

# Day 4: PriceHistory Aggregate

## 🧒 Explain Like I'm 5

Imagine you have a notebook 📓 where you write down the price of flour every time mom buys it:
- January 1: Flour was Rp 14,000
- January 15: Flour was Rp 15,000 (went UP!)
- February 1: Flour was Rp 15,500 (went UP again!)

**PriceHistory** is like that notebook — it remembers all the prices!

## 🔧 Engineer Language

**PriceHistory** tracks price changes for ingredients over time. This is crucial for:
- Detecting price spikes (alerts)
- Showing price trends (analytics)
- Calculating cost changes in recipes

### PriceHistory Aggregate Implementation:

Create `src/Nastart.Domain/Finance/Aggregates/PriceHistory.cs`:

```csharp
using Nastart.Domain.Common;
using Nastart.Domain.Finance.ValueObjects;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Domain.Finance.Aggregates;

/// <summary>
/// Aggregate for tracking price history of ingredients.
/// Each record represents a price point at a specific date.
/// </summary>
public class PriceHistory : AggregateRoot
{
    public PriceHistoryId HistoryId { get; private set; }
    public IngredientId IngredientId { get; private set; }
    public Guid ShopId { get; private set; }
    public DateOnly RecordedDate { get; private set; }
    public Money Price { get; private set; }
    public Money? PreviousPrice { get; private set; }
    public Percentage? ChangePercentage { get; private set; }
    public PurchaseId SourcePurchaseId { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
    private PriceHistory()
    {
        HistoryId = PriceHistoryId.Empty;
        IngredientId = IngredientId.Empty;
        Price = Money.Zero;
        SourcePurchaseId = PurchaseId.Empty;
    }

    /// <summary>
    /// Creates a new price history record.
    /// </summary>
    public PriceHistory(
        IngredientId ingredientId,
        Guid shopId,
        DateOnly recordedDate,
        Money price,
        Money? previousPrice,
        PurchaseId sourcePurchaseId)
    {
        if (ingredientId.IsEmpty)
            throw new ArgumentException("Ingredient ID is required.", nameof(ingredientId));
        
        if (price.Amount < 0)
            throw new ArgumentException("Price cannot be negative.", nameof(price));

        HistoryId = PriceHistoryId.New();
        IngredientId = ingredientId;
        ShopId = shopId;
        RecordedDate = recordedDate;
        Price = price;
        PreviousPrice = previousPrice;
        SourcePurchaseId = sourcePurchaseId;

        CalculateChangePercentage();
    }

    /// <summary>
    /// Calculates the percentage change from previous price.
    /// </summary>
    private void CalculateChangePercentage()
    {
        if (PreviousPrice is null || PreviousPrice.Amount == 0)
        {
            ChangePercentage = null;
            return;
        }

        var change = ((Price.Amount - PreviousPrice.Amount) / PreviousPrice.Amount) * 100;
        ChangePercentage = new Percentage(Math.Round(change, 2));
    }

    /// <summary>
    /// Checks if this price represents a significant increase (spike).
    /// </summary>
    /// <param name="spikeThreshold">Percentage threshold (default 15%).</param>
    public bool IsPriceSpike(decimal spikeThreshold = 15m)
    {
        return ChangePercentage is not null && ChangePercentage.IsAbove(spikeThreshold);
    }

    /// <summary>
    /// Checks if this price represents a significant decrease.
    /// </summary>
    /// <param name="dropThreshold">Percentage threshold (default -10%).</param>
    public bool IsPriceDrop(decimal dropThreshold = -10m)
    {
        return ChangePercentage is not null && ChangePercentage.Value < dropThreshold;
    }
}
```

### Your Task (Day 4):

1. Create the `PriceHistory.cs` file
2. Build to verify: `dotnet build`

---

# Day 5: Domain Service — PriceAnalysisService

## 🧒 Explain Like I'm 5

Remember how a calculator helps you do math? 🧮

**PriceAnalysisService** is like a calculator for prices. It looks at all the prices and tells you:
- "This flour price went UP by 20%! ⚠️"
- "Sugar has been getting more expensive over 30 days"
- "Good news! Eggs are cheaper now 🎉"

## 🔧 Engineer Language

A **Domain Service** contains domain logic that:
1. Doesn't naturally belong to any single entity/aggregate
2. Requires data from multiple aggregates
3. Performs calculations or analysis

> 📖 **Microsoft Docs**: *"Domain entities must implement behavior in addition to implementing data attributes. [...] The entity's methods take care of the invariants and rules of the entity."*
>
> For cross-aggregate logic, we use Domain Services.
>
> — [Design a microservice domain model](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model#the-domain-entity-pattern)

### Key Characteristics of Domain Services:

| Characteristic | PriceAnalysisService |
|----------------|---------------------|
| **Stateless** | No internal state, pure functions |
| **Domain logic** | Price spike detection, trend analysis |
| **Cross-aggregate** | Analyzes data from PriceHistory, Purchase |
| **Named with domain term** | "Price Analysis" is ubiquitous language |

### PriceAnalysisService Implementation:

Create `src/Nastart.Domain/Finance/Services/PriceAnalysisService.cs`:

```csharp
using Nastart.Domain.Finance.Aggregates;
using Nastart.Domain.Finance.ValueObjects;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Domain.Finance.Services;

/// <summary>
/// Domain service for analyzing price changes and detecting anomalies.
/// Stateless — contains pure business logic for price analysis.
/// </summary>
/// <remarks>
/// Domain services contain logic that doesn't belong to a single aggregate.
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice
/// </remarks>
public class PriceAnalysisService : IPriceAnalysisService
{
    /// <summary>
    /// Default threshold for price spike detection (15%).
    /// Configured in Nastart as per business requirements.
    /// </summary>
    public const decimal DefaultSpikeThreshold = 15m;

    /// <summary>
    /// Default threshold for inflation trend detection over time (10%).
    /// </summary>
    public const decimal DefaultInflationThreshold = 10m;

    /// <summary>
    /// Default number of days for trend analysis.
    /// </summary>
    public const int DefaultTrendDays = 30;

    /// <summary>
    /// Analyzes a new price against the previous price to detect a spike.
    /// </summary>
    /// <param name="currentPrice">The new/current price.</param>
    /// <param name="previousPrice">The previous/old price.</param>
    /// <param name="spikeThreshold">Percentage threshold for spike (default 15%).</param>
    /// <returns>Analysis result with spike detection info.</returns>
    public PriceAnalysisResult AnalyzePriceChange(
        Money currentPrice,
        Money? previousPrice,
        decimal spikeThreshold = DefaultSpikeThreshold)
    {
        if (previousPrice is null || previousPrice.Amount == 0)
        {
            return new PriceAnalysisResult(
                CurrentPrice: currentPrice,
                PreviousPrice: previousPrice,
                ChangeAmount: null,
                ChangePercentage: null,
                IsSpike: false,
                IsDrop: false,
                AnalysisType: PriceAnalysisType.FirstRecord);
        }

        var changeAmount = currentPrice.Amount - previousPrice.Amount;
        var changePercentage = (changeAmount / previousPrice.Amount) * 100;
        var roundedPercentage = Math.Round(changePercentage, 2);

        var isSpike = roundedPercentage >= spikeThreshold;
        var isDrop = roundedPercentage <= -spikeThreshold;

        return new PriceAnalysisResult(
            CurrentPrice: currentPrice,
            PreviousPrice: previousPrice,
            ChangeAmount: new Money(changeAmount, currentPrice.Currency),
            ChangePercentage: new Percentage(roundedPercentage),
            IsSpike: isSpike,
            IsDrop: isDrop,
            AnalysisType: isSpike ? PriceAnalysisType.Spike :
                          isDrop ? PriceAnalysisType.Drop :
                          PriceAnalysisType.Normal);
    }

    /// <summary>
    /// Analyzes price trend over a period using historical data.
    /// </summary>
    /// <param name="priceHistory">Historical price records, ordered by date.</param>
    /// <param name="inflationThreshold">Threshold for inflation alert (default 10%).</param>
    /// <returns>Trend analysis result.</returns>
    public PriceTrendResult AnalyzeTrend(
        IReadOnlyList<PriceHistory> priceHistory,
        decimal inflationThreshold = DefaultInflationThreshold)
    {
        if (priceHistory.Count < 2)
        {
            return new PriceTrendResult(
                StartPrice: priceHistory.FirstOrDefault()?.Price,
                EndPrice: priceHistory.LastOrDefault()?.Price,
                TotalChangePercentage: null,
                AveragePrice: priceHistory.FirstOrDefault()?.Price,
                TrendDirection: TrendDirection.Stable,
                IsInflationAlert: false,
                DataPoints: priceHistory.Count);
        }

        var orderedHistory = priceHistory.OrderBy(h => h.RecordedDate).ToList();
        var startPrice = orderedHistory.First().Price;
        var endPrice = orderedHistory.Last().Price;

        var totalChange = ((endPrice.Amount - startPrice.Amount) / startPrice.Amount) * 100;
        var roundedChange = Math.Round(totalChange, 2);

        var avgPrice = orderedHistory.Average(h => h.Price.Amount);
        var averagePrice = new Money(Math.Round(avgPrice, 2), startPrice.Currency);

        var trendDirection = roundedChange switch
        {
            > 5 => TrendDirection.Rising,
            < -5 => TrendDirection.Falling,
            _ => TrendDirection.Stable
        };

        var isInflationAlert = roundedChange >= inflationThreshold;

        return new PriceTrendResult(
            StartPrice: startPrice,
            EndPrice: endPrice,
            TotalChangePercentage: new Percentage(roundedChange),
            AveragePrice: averagePrice,
            TrendDirection: trendDirection,
            IsInflationAlert: isInflationAlert,
            DataPoints: priceHistory.Count);
    }

    /// <summary>
    /// Detects all ingredients with price spikes from a list of recent price changes.
    /// </summary>
    /// <param name="recentPriceHistory">Recent price history records.</param>
    /// <param name="spikeThreshold">Threshold for spike detection.</param>
    /// <returns>List of ingredients with detected spikes.</returns>
    public IReadOnlyList<PriceSpikeInfo> DetectSpikes(
        IEnumerable<PriceHistory> recentPriceHistory,
        decimal spikeThreshold = DefaultSpikeThreshold)
    {
        return recentPriceHistory
            .Where(h => h.IsPriceSpike(spikeThreshold))
            .Select(h => new PriceSpikeInfo(
                IngredientId: h.IngredientId,
                ShopId: h.ShopId,
                RecordedDate: h.RecordedDate,
                CurrentPrice: h.Price,
                PreviousPrice: h.PreviousPrice!,
                ChangePercentage: h.ChangePercentage!,
                SourcePurchaseId: h.SourcePurchaseId))
            .ToList();
    }
}

/// <summary>
/// Interface for price analysis domain service.
/// </summary>
public interface IPriceAnalysisService
{
    PriceAnalysisResult AnalyzePriceChange(Money currentPrice, Money? previousPrice, decimal spikeThreshold = 15m);
    PriceTrendResult AnalyzeTrend(IReadOnlyList<PriceHistory> priceHistory, decimal inflationThreshold = 10m);
    IReadOnlyList<PriceSpikeInfo> DetectSpikes(IEnumerable<PriceHistory> recentPriceHistory, decimal spikeThreshold = 15m);
}

/// <summary>
/// Result of single price change analysis.
/// </summary>
public sealed record PriceAnalysisResult(
    Money CurrentPrice,
    Money? PreviousPrice,
    Money? ChangeAmount,
    Percentage? ChangePercentage,
    bool IsSpike,
    bool IsDrop,
    PriceAnalysisType AnalysisType);

/// <summary>
/// Result of price trend analysis over time.
/// </summary>
public sealed record PriceTrendResult(
    Money? StartPrice,
    Money? EndPrice,
    Percentage? TotalChangePercentage,
    Money? AveragePrice,
    TrendDirection TrendDirection,
    bool IsInflationAlert,
    int DataPoints);

/// <summary>
/// Information about a detected price spike.
/// </summary>
public sealed record PriceSpikeInfo(
    IngredientId IngredientId,
    Guid ShopId,
    DateOnly RecordedDate,
    Money CurrentPrice,
    Money PreviousPrice,
    Percentage ChangePercentage,
    PurchaseId SourcePurchaseId);

/// <summary>
/// Type of price analysis result.
/// </summary>
public enum PriceAnalysisType
{
    /// <summary>First price record, no comparison available.</summary>
    FirstRecord,
    
    /// <summary>Price is within normal range.</summary>
    Normal,
    
    /// <summary>Price increased significantly (spike).</summary>
    Spike,
    
    /// <summary>Price decreased significantly.</summary>
    Drop
}

/// <summary>
/// Direction of price trend over time.
/// </summary>
public enum TrendDirection
{
    /// <summary>Prices are relatively stable.</summary>
    Stable,
    
    /// <summary>Prices are rising.</summary>
    Rising,
    
    /// <summary>Prices are falling.</summary>
    Falling
}
```

### Your Task (Day 5):

1. Create the `PriceAnalysisService.cs` file
2. Build to verify: `dotnet build`
3. Note how the service is **stateless** — it receives data as parameters

---

# Day 6: Domain Events — PriceSpikeDetectedEvent

## 🧒 Explain Like I'm 5

When something important happens, we shout about it! 📢

- When flour price goes UP a lot → "HEY! FLOUR IS EXPENSIVE NOW!"
- When you buy something → "HEY! I JUST BOUGHT STUFF!"

**Domain Events** are like those shouts — they tell other parts of the app when something important happened.

## 🔧 Engineer Language

**Domain Events** capture something that happened in the domain that domain experts care about. They:
- Are past-tense ("PriceSpikeDetected", not "DetectPriceSpike")
- Carry data about what happened
- Trigger side effects (notifications, updates)

> 📖 **Microsoft Docs**: *"Use domain events to explicitly implement side effects of changes within your domain. [...] Domain events help you to express, explicitly, the domain rules, based in the ubiquitous language provided by the domain experts."*
>
> — [Domain events: Design and implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)

### Finance Domain Events:

Create `src/Nastart.Domain/Finance/Events/FinanceEvents.cs`:

```csharp
using MediatR;
using Nastart.Domain.Finance.ValueObjects;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Domain.Finance.Events;

/// <summary>
/// Raised when a purchase is recorded and confirmed.
/// Triggers price tracking and inventory updates.
/// </summary>
/// <remarks>
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation
/// </remarks>
public sealed record PurchaseRecordedEvent(
    PurchaseId PurchaseId,
    Guid UserId,
    Guid ShopId,
    DateOnly PurchaseDate,
    Money Total,
    IReadOnlyList<PurchaseRecordedEvent.ItemInfo> Items) : INotification
{
    /// <summary>
    /// Information about each purchased item.
    /// </summary>
    public sealed record ItemInfo(
        IngredientId IngredientId,
        string IngredientName,
        Money UnitPrice);
}

/// <summary>
/// Raised when a significant price increase is detected.
/// Triggers alerts to the user.
/// </summary>
public sealed record PriceSpikeDetectedEvent(
    IngredientId IngredientId,
    string IngredientName,
    Guid UserId,
    Guid ShopId,
    DateOnly DetectedDate,
    Money CurrentPrice,
    Money PreviousPrice,
    Percentage ChangePercentage,
    PurchaseId SourcePurchaseId) : INotification
{
    /// <summary>
    /// Gets a human-readable description of the spike.
    /// </summary>
    public string Description => 
        $"{IngredientName} price increased by {ChangePercentage} " +
        $"(from {PreviousPrice} to {CurrentPrice})";
}

/// <summary>
/// Raised when price trend analysis detects inflation.
/// Triggers weekly/monthly reports.
/// </summary>
public sealed record InflationTrendDetectedEvent(
    IngredientId IngredientId,
    string IngredientName,
    Guid UserId,
    DateOnly StartDate,
    DateOnly EndDate,
    Money StartPrice,
    Money EndPrice,
    Percentage TotalChangePercentage) : INotification;

/// <summary>
/// Raised when an ingredient price is updated.
/// Triggers recipe cost recalculation.
/// </summary>
public sealed record IngredientPriceUpdatedEvent(
    IngredientId IngredientId,
    Money NewPrice,
    Money? PreviousPrice,
    DateOnly UpdatedDate,
    PurchaseId SourcePurchaseId) : INotification;
```

### Event Flow Diagram:

```
┌─────────────────┐
│   Purchase      │
│   .Confirm()    │
└────────┬────────┘
         │ raises
         ▼
┌─────────────────────────────────┐
│  PurchaseRecordedEvent          │
│  - PurchaseId                   │
│  - Items[] with prices          │
└────────┬────────────────────────┘
         │ handled by
         ▼
┌─────────────────────────────────┐
│  PriceTrackingHandler           │
│  (Application Layer)            │
│  1. Create PriceHistory         │
│  2. Call PriceAnalysisService   │
│  3. If spike → raise event      │
└────────┬────────────────────────┘
         │ raises
         ▼
┌─────────────────────────────────┐
│  PriceSpikeDetectedEvent        │
│  - IngredientId                 │
│  - ChangePercentage             │
└────────┬────────────────────────┘
         │ handled by
         ▼
┌─────────────────────────────────┐
│  AlertNotificationHandler       │
│  1. Create Alert in DB          │
│  2. Send Telegram notification  │
└─────────────────────────────────┘
```

### Your Task (Day 6):

1. Create the `FinanceEvents.cs` file
2. Build to verify: `dotnet build`

---

# Day 7: Unit Testing Finance Domain

## 🧒 Explain Like I'm 5

Before you give a toy to your friend, you TEST it first:
- Does the car's wheels spin? ✅
- Does the doll's arms move? ✅

**Unit Tests** are like testing toys before giving them away. We make sure our code works correctly!

## 🔧 Engineer Language

Unit tests verify that individual units of code (classes, methods) work as expected in isolation.

> 📖 **Microsoft Docs**: *"Your domain model should not take direct dependencies on any infrastructure framework like Entity Framework. [...] This makes it easier to test the domain model."*
>
> — [DDD-oriented microservice](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice#layers-in-ddd-microservices)

### Test Project Setup:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend

# Add reference to Domain project
dotnet add tests/Nastart.Domain.Tests reference src/Nastart.Domain

# Add FluentAssertions for readable assertions
dotnet add tests/Nastart.Domain.Tests package FluentAssertions
```

### Test: Strongly-Typed IDs

Create `tests/Nastart.Domain.Tests/Finance/StronglyTypedIdTests.cs`:

```csharp
using FluentAssertions;
using Nastart.Domain.Finance.ValueObjects;

namespace Nastart.Domain.Tests.Finance;

/// <summary>
/// Tests for strongly-typed ID value objects.
/// </summary>
public class StronglyTypedIdTests
{
    [Fact]
    public void PurchaseId_New_ShouldCreateUniqueId()
    {
        // Arrange & Act
        var id1 = PurchaseId.New();
        var id2 = PurchaseId.New();

        // Assert
        id1.Should().NotBe(id2);
        id1.Value.Should().NotBeEmpty();
        id2.Value.Should().NotBeEmpty();
    }

    [Fact]
    public void PurchaseId_Empty_ShouldHaveEmptyGuid()
    {
        // Arrange & Act
        var emptyId = PurchaseId.Empty;

        // Assert
        emptyId.Value.Should().Be(Guid.Empty);
        emptyId.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void PurchaseId_Equality_ShouldCompareByValue()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var id1 = new PurchaseId(guid);
        var id2 = new PurchaseId(guid);

        // Assert
        id1.Should().Be(id2);
        (id1 == id2).Should().BeTrue();
    }

    [Fact]
    public void PurchaseId_ImplicitConversion_ShouldReturnGuid()
    {
        // Arrange
        var expectedGuid = Guid.NewGuid();
        var purchaseId = new PurchaseId(expectedGuid);

        // Act
        Guid actualGuid = purchaseId;  // Implicit conversion

        // Assert
        actualGuid.Should().Be(expectedGuid);
    }
}
```

### Test: Purchase Aggregate

Create `tests/Nastart.Domain.Tests/Finance/PurchaseAggregateTests.cs`:

```csharp
using FluentAssertions;
using Nastart.Domain.Finance.Aggregates;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Domain.Tests.Finance;

/// <summary>
/// Tests for Purchase aggregate root.
/// </summary>
public class PurchaseAggregateTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _shopId = Guid.NewGuid();
    private readonly DateOnly _purchaseDate = DateOnly.FromDateTime(DateTime.Today);

    [Fact]
    public void Constructor_WithValidData_ShouldCreatePurchase()
    {
        // Act
        var purchase = new Purchase(_userId, _shopId, _purchaseDate);

        // Assert
        purchase.PurchaseId.IsEmpty.Should().BeFalse();
        purchase.UserId.Should().Be(_userId);
        purchase.ShopId.Should().Be(_shopId);
        purchase.PurchaseDate.Should().Be(_purchaseDate);
        purchase.Status.Should().Be(PurchaseStatus.Draft);
        purchase.Total.IsZero.Should().BeTrue();
        purchase.PurchaseItems.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithEmptyUserId_ShouldThrow()
    {
        // Act
        var act = () => new Purchase(Guid.Empty, _shopId, _purchaseDate);

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithMessage("*User ID*");
    }

    [Fact]
    public void AddItem_ShouldAddItemAndRecalculateTotal()
    {
        // Arrange
        var purchase = new Purchase(_userId, _shopId, _purchaseDate);
        var ingredientId = IngredientId.New();
        var quantity = Quantity.Kilograms(2);
        var unitPrice = Money.FromIDR(15000);

        // Act
        var item = purchase.AddItem(ingredientId, "Flour", quantity, unitPrice);

        // Assert
        purchase.PurchaseItems.Should().HaveCount(1);
        purchase.PurchaseItems.Should().Contain(item);
        purchase.Total.Amount.Should().Be(30000m); // 2 × 15000
        item.LineTotal.Amount.Should().Be(30000m);
    }

    [Fact]
    public void AddItem_WithDuplicateIngredient_ShouldUpdateExistingItem()
    {
        // Arrange
        var purchase = new Purchase(_userId, _shopId, _purchaseDate);
        var ingredientId = IngredientId.New();
        var quantity1 = Quantity.Kilograms(1);
        var quantity2 = Quantity.Kilograms(2);
        var unitPrice = Money.FromIDR(15000);

        // Act
        purchase.AddItem(ingredientId, "Flour", quantity1, unitPrice);
        purchase.AddItem(ingredientId, "Flour", quantity2, unitPrice);

        // Assert
        purchase.PurchaseItems.Should().HaveCount(1);
        purchase.PurchaseItems.First().Quantity.Value.Should().Be(3m); // 1 + 2
        purchase.Total.Amount.Should().Be(45000m); // 3 × 15000
    }

    [Fact]
    public void AddItem_MultipleItems_ShouldCalculateTotalCorrectly()
    {
        // Arrange
        var purchase = new Purchase(_userId, _shopId, _purchaseDate);

        // Act
        purchase.AddItem(IngredientId.New(), "Flour", Quantity.Kilograms(2), Money.FromIDR(15000));
        purchase.AddItem(IngredientId.New(), "Sugar", Quantity.Kilograms(1), Money.FromIDR(12000));
        purchase.AddItem(IngredientId.New(), "Eggs", Quantity.Pieces(10), Money.FromIDR(2500));

        // Assert
        purchase.PurchaseItems.Should().HaveCount(3);
        // Total = (2×15000) + (1×12000) + (10×2500) = 30000 + 12000 + 25000 = 67000
        purchase.Total.Amount.Should().Be(67000m);
    }

    [Fact]
    public void RemoveItem_ShouldRemoveAndRecalculateTotal()
    {
        // Arrange
        var purchase = new Purchase(_userId, _shopId, _purchaseDate);
        var item = purchase.AddItem(IngredientId.New(), "Flour", Quantity.Kilograms(2), Money.FromIDR(15000));
        purchase.AddItem(IngredientId.New(), "Sugar", Quantity.Kilograms(1), Money.FromIDR(12000));

        // Act
        purchase.RemoveItem(item.ItemId);

        // Assert
        purchase.PurchaseItems.Should().HaveCount(1);
        purchase.Total.Amount.Should().Be(12000m); // Only sugar remains
    }

    [Fact]
    public void Confirm_ShouldChangStatusAndRaiseDomainEvent()
    {
        // Arrange
        var purchase = new Purchase(_userId, _shopId, _purchaseDate);
        purchase.AddItem(IngredientId.New(), "Flour", Quantity.Kilograms(2), Money.FromIDR(15000));

        // Act
        purchase.Confirm();

        // Assert
        purchase.Status.Should().Be(PurchaseStatus.Confirmed);
        purchase.DomainEvents.Should().HaveCount(1);
        purchase.DomainEvents.Should().ContainItemsAssignableTo<INotification>();
    }

    [Fact]
    public void Confirm_WithEmptyPurchase_ShouldThrow()
    {
        // Arrange
        var purchase = new Purchase(_userId, _shopId, _purchaseDate);

        // Act
        var act = () => purchase.Confirm();

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*empty purchase*");
    }

    [Fact]
    public void AddItem_AfterConfirm_ShouldThrow()
    {
        // Arrange
        var purchase = new Purchase(_userId, _shopId, _purchaseDate);
        purchase.AddItem(IngredientId.New(), "Flour", Quantity.Kilograms(2), Money.FromIDR(15000));
        purchase.Confirm();

        // Act
        var act = () => purchase.AddItem(IngredientId.New(), "Sugar", Quantity.Kilograms(1), Money.FromIDR(12000));

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*confirmed purchase*");
    }
}
```

### Test: PriceAnalysisService

Create `tests/Nastart.Domain.Tests/Finance/PriceAnalysisServiceTests.cs`:

```csharp
using FluentAssertions;
using Nastart.Domain.Finance.Aggregates;
using Nastart.Domain.Finance.Services;
using Nastart.Domain.Finance.ValueObjects;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Domain.Tests.Finance;

/// <summary>
/// Tests for PriceAnalysisService domain service.
/// </summary>
public class PriceAnalysisServiceTests
{
    private readonly PriceAnalysisService _service = new();

    [Fact]
    public void AnalyzePriceChange_FirstRecord_ShouldReturnFirstRecordType()
    {
        // Arrange
        var currentPrice = Money.FromIDR(15000);

        // Act
        var result = _service.AnalyzePriceChange(currentPrice, previousPrice: null);

        // Assert
        result.AnalysisType.Should().Be(PriceAnalysisType.FirstRecord);
        result.IsSpike.Should().BeFalse();
        result.ChangePercentage.Should().BeNull();
    }

    [Fact]
    public void AnalyzePriceChange_PriceIncreaseBelow15Percent_ShouldNotBeSpike()
    {
        // Arrange
        var previousPrice = Money.FromIDR(10000);
        var currentPrice = Money.FromIDR(11000); // 10% increase

        // Act
        var result = _service.AnalyzePriceChange(currentPrice, previousPrice);

        // Assert
        result.IsSpike.Should().BeFalse();
        result.AnalysisType.Should().Be(PriceAnalysisType.Normal);
        result.ChangePercentage!.Value.Should().Be(10m);
    }

    [Fact]
    public void AnalyzePriceChange_PriceIncreaseAt15Percent_ShouldBeSpike()
    {
        // Arrange
        var previousPrice = Money.FromIDR(10000);
        var currentPrice = Money.FromIDR(11500); // 15% increase

        // Act
        var result = _service.AnalyzePriceChange(currentPrice, previousPrice);

        // Assert
        result.IsSpike.Should().BeTrue();
        result.AnalysisType.Should().Be(PriceAnalysisType.Spike);
        result.ChangePercentage!.Value.Should().Be(15m);
    }

    [Fact]
    public void AnalyzePriceChange_PriceIncreaseAbove15Percent_ShouldBeSpike()
    {
        // Arrange
        var previousPrice = Money.FromIDR(10000);
        var currentPrice = Money.FromIDR(12000); // 20% increase

        // Act
        var result = _service.AnalyzePriceChange(currentPrice, previousPrice);

        // Assert
        result.IsSpike.Should().BeTrue();
        result.AnalysisType.Should().Be(PriceAnalysisType.Spike);
        result.ChangePercentage!.Value.Should().Be(20m);
        result.ChangeAmount!.Amount.Should().Be(2000m);
    }

    [Fact]
    public void AnalyzePriceChange_PriceDecrease_ShouldBeDrop()
    {
        // Arrange
        var previousPrice = Money.FromIDR(10000);
        var currentPrice = Money.FromIDR(8000); // 20% decrease

        // Act
        var result = _service.AnalyzePriceChange(currentPrice, previousPrice);

        // Assert
        result.IsDrop.Should().BeTrue();
        result.AnalysisType.Should().Be(PriceAnalysisType.Drop);
        result.ChangePercentage!.Value.Should().Be(-20m);
    }

    [Fact]
    public void AnalyzePriceChange_WithCustomThreshold_ShouldUseThreshold()
    {
        // Arrange
        var previousPrice = Money.FromIDR(10000);
        var currentPrice = Money.FromIDR(10500); // 5% increase

        // Act
        var result = _service.AnalyzePriceChange(currentPrice, previousPrice, spikeThreshold: 5m);

        // Assert
        result.IsSpike.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeTrend_WithRisingPrices_ShouldDetectRisingTrend()
    {
        // Arrange
        var ingredientId = IngredientId.New();
        var shopId = Guid.NewGuid();
        var purchaseId = PurchaseId.New();

        var history = new List<PriceHistory>
        {
            CreatePriceHistory(ingredientId, shopId, DateOnly.Parse("2026-01-01"), 10000m, null, purchaseId),
            CreatePriceHistory(ingredientId, shopId, DateOnly.Parse("2026-01-15"), 10500m, 10000m, purchaseId),
            CreatePriceHistory(ingredientId, shopId, DateOnly.Parse("2026-01-30"), 11200m, 10500m, purchaseId)
        };

        // Act
        var result = _service.AnalyzeTrend(history);

        // Assert
        result.TrendDirection.Should().Be(TrendDirection.Rising);
        result.TotalChangePercentage!.Value.Should().Be(12m); // (11200-10000)/10000 = 12%
        result.IsInflationAlert.Should().BeTrue(); // 12% > 10% threshold
        result.DataPoints.Should().Be(3);
    }

    [Fact]
    public void AnalyzeTrend_WithStablePrices_ShouldDetectStableTrend()
    {
        // Arrange
        var ingredientId = IngredientId.New();
        var shopId = Guid.NewGuid();
        var purchaseId = PurchaseId.New();

        var history = new List<PriceHistory>
        {
            CreatePriceHistory(ingredientId, shopId, DateOnly.Parse("2026-01-01"), 10000m, null, purchaseId),
            CreatePriceHistory(ingredientId, shopId, DateOnly.Parse("2026-01-15"), 10200m, 10000m, purchaseId),
            CreatePriceHistory(ingredientId, shopId, DateOnly.Parse("2026-01-30"), 10100m, 10200m, purchaseId)
        };

        // Act
        var result = _service.AnalyzeTrend(history);

        // Assert
        result.TrendDirection.Should().Be(TrendDirection.Stable);
        result.IsInflationAlert.Should().BeFalse();
    }

    [Fact]
    public void DetectSpikes_ShouldReturnOnlySpikes()
    {
        // Arrange
        var ingredientId1 = IngredientId.New();
        var ingredientId2 = IngredientId.New();
        var shopId = Guid.NewGuid();
        var purchaseId = PurchaseId.New();

        var history = new List<PriceHistory>
        {
            // This is a spike (20% increase)
            CreatePriceHistory(ingredientId1, shopId, DateOnly.Parse("2026-01-30"), 12000m, 10000m, purchaseId),
            // This is NOT a spike (5% increase)
            CreatePriceHistory(ingredientId2, shopId, DateOnly.Parse("2026-01-30"), 10500m, 10000m, purchaseId)
        };

        // Act
        var spikes = _service.DetectSpikes(history);

        // Assert
        spikes.Should().HaveCount(1);
        spikes[0].IngredientId.Should().Be(ingredientId1);
        spikes[0].ChangePercentage.Value.Should().Be(20m);
    }

    private static PriceHistory CreatePriceHistory(
        IngredientId ingredientId,
        Guid shopId,
        DateOnly date,
        decimal price,
        decimal? previousPrice,
        PurchaseId purchaseId)
    {
        return new PriceHistory(
            ingredientId,
            shopId,
            date,
            Money.FromIDR(price),
            previousPrice.HasValue ? Money.FromIDR(previousPrice.Value) : null,
            purchaseId);
    }
}
```

### Run Tests:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend
dotnet test tests/Nastart.Domain.Tests --verbosity normal
```

### Your Task (Day 7):

1. Create all test files
2. Run tests and ensure they pass
3. Review test coverage for edge cases

---

# Resources

## Microsoft Official Documentation (Verified ✓)

| Topic | Link |
|-------|------|
| **Strongly-Typed IDs in EF Core** | [What's New in EF Core 7.0 - Value Generation for DDD](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-7.0/whatsnew#value-generation-for-ddd-guarded-types) |
| **Aggregate Pattern** | [Design a microservice domain model](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model) |
| **Entity Encapsulation** | [Implement a microservice domain model with .NET](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/net-core-microservice-domain-model) |
| **Domain Events** | [Domain events: Design and implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation) |
| **DDD Layers** | [Design a DDD-oriented microservice](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice) |
| **SeedWork Pattern** | [Seedwork base classes and interfaces](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/seedwork-domain-model-base-classes-interfaces) |

## Additional Reading

| Resource | Description |
|----------|-------------|
| [eShopOnContainers](https://github.com/dotnet-architecture/eShopOnContainers) | Reference implementation from Microsoft |
| [Vaughn Vernon - Effective Aggregate Design](https://dddcommunity.org/wp-content/uploads/files/pdf_articles/Vernon_2011_1.pdf) | Deep dive into aggregate design |
| [.NET Microservices eBook](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/) | Free Microsoft eBook |

---

## Week 3 Summary

### What You Built:

| Component | Type | Purpose |
|-----------|------|---------|
| `PurchaseId`, `SaleId`, `PriceHistoryId` | Strongly-Typed IDs | Type-safe entity identification |
| `Purchase` | Aggregate Root | Shopping transaction with items |
| `PurchaseItem` | Child Entity | Line items within purchase |
| `PriceHistory` | Aggregate Root | Price tracking over time |
| `PriceAnalysisService` | Domain Service | Price spike & trend detection |
| `PriceSpikeDetectedEvent` | Domain Event | Notification trigger |

### Folder Structure After Week 3:

```
src/Nastart.Domain/
├── Common/                        # Week 2
│   ├── Entity.cs
│   ├── AggregateRoot.cs
│   └── ValueObject.cs
├── Finance/                       # Week 3 ✨
│   ├── Aggregates/
│   │   ├── Purchase.cs
│   │   └── PriceHistory.cs
│   ├── Entities/
│   │   └── PurchaseItem.cs
│   ├── ValueObjects/
│   │   └── FinanceIds.cs
│   ├── Events/
│   │   └── FinanceEvents.cs
│   └── Services/
│       └── PriceAnalysisService.cs
└── Inventory/                     # Week 2
    ├── Aggregates/
    ├── ValueObjects/
    └── Events/
```

### Next Week Preview (Week 4 — Recipe Context):

- [ ] Build `Recipe` aggregate with `RecipeItem` collection
- [ ] Create `Margin` value object
- [ ] Implement `CostCalculationService`
- [ ] Write `MarginBelowThresholdEvent`

---

*Document verified with Microsoft Learn docs — January 30, 2026*
