# Week 2: Domain Layer Foundation 🏗️

> **Goal**: Build the reusable building blocks for your entire domain model — Entity, AggregateRoot, ValueObject, and your first real aggregate: `Ingredient`.

---

## Table of Contents
1. [Day 1: The SeedWork Pattern](#day-1-the-seedwork-pattern)
2. [Day 2: Entity Base Class](#day-2-entity-base-class)
3. [Day 3: Value Objects](#day-3-value-objects)
4. [Day 4: Domain Events](#day-4-domain-events)
5. [Day 5: Aggregate Root Pattern](#day-5-aggregate-root-pattern)
6. [Day 6: Building the Ingredient Aggregate](#day-6-building-the-ingredient-aggregate)
7. [Day 7: Unit Testing Your Domain](#day-7-unit-testing-your-domain)
8. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1: The SeedWork Pattern

## 🧒 Explain Like I'm 5

Imagine you're building many different LEGO houses. Instead of making a new door from scratch every time, you have a **special box of door pieces** that work for ALL houses!

**SeedWork** is like that special box. It has pieces that every part of your app needs:
- Base classes for all your "things" (entities)
- Rules for comparing things
- Ways to send messages between parts

## 🔧 Engineer Language

**SeedWork** (also called "Common" or "SharedKernel") is a folder containing reusable base classes and interfaces that form the foundation of your domain model. The term was popularized by Martin Fowler.

> 📖 **Microsoft Docs**: *"The folder for these types of classes is called SeedWork and not something like Framework. It's called SeedWork because the folder contains just a small subset of reusable classes that cannot really be considered a framework."*
> 
> — [Seedwork base classes and interfaces](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/seedwork-domain-model-base-classes-interfaces)

### What Goes in SeedWork:

| File | Purpose |
|------|---------|
| `Entity.cs` | Base class for all entities with ID and equality |
| `AggregateRoot.cs` | Marker interface + base for aggregate roots |
| `ValueObject.cs` | Base class for value objects |
| `IDomainEvent.cs` | Interface for domain events |
| `IRepository.cs` | Generic repository interface |
| `IUnitOfWork.cs` | Unit of work pattern interface |
| `Enumeration.cs` | Base for smart enums (optional) |

### Folder Structure:

```
src/Nastart.Domain/
├── Common/                    # ← This is your SeedWork folder
│   ├── Entity.cs
│   ├── AggregateRoot.cs
│   ├── IAggregateRoot.cs
│   ├── ValueObject.cs
│   ├── IDomainEvent.cs
│   ├── IRepository.cs
│   └── IUnitOfWork.cs
├── Inventory/
│   ├── Aggregates/
│   │   └── Ingredient.cs
│   ├── ValueObjects/
│   │   └── Money.cs
│   └── Events/
│       └── PriceChangedEvent.cs
└── ...
```

### Your Task (Day 1):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Domain

# Create folder structure
mkdir Common
mkdir Inventory\Aggregates
mkdir Inventory\ValueObjects
mkdir Inventory\Events
mkdir Recipe\Aggregates
mkdir Finance\Aggregates
mkdir Finance\Events

# Remove placeholder file
Remove-Item Class1.cs -ErrorAction SilentlyContinue
```

---

# Day 2: Entity Base Class

## 🧒 Explain Like I'm 5

Every person has a unique ID number (like a student ID). Even if two people have the same name, they're DIFFERENT people because their IDs are different.

In our app, **Entities** are "things" with an ID:
- **Ingredient** with ID 123 is DIFFERENT from Ingredient with ID 456
- Even if both are called "Flour", they're tracked separately

## 🔧 Engineer Language

An **Entity** is a domain object that has:
1. **Identity** — A unique identifier that persists over time
2. **Lifecycle** — It can be created, modified, and deleted
3. **Equality by ID** — Two entities are equal if their IDs match

> 📖 **Microsoft Docs**: *"An entity is an object with a unique identity that persists over time. For example, in a banking application, customers and accounts would be entities."*
>
> — [Tactical DDD Patterns](https://learn.microsoft.com/en-us/azure/architecture/microservices/model/tactical-ddd#overview-of-the-tactical-patterns)

### Entity Base Class (from eShopOnContainers):

Create `src/Nastart.Domain/Common/Entity.cs`:

```csharp
using MediatR;

namespace Nastart.Domain.Common;

/// <summary>
/// Base class for all domain entities.
/// Provides ID, equality, and domain event support.
/// </summary>
/// <remarks>
/// Based on Microsoft's eShopOnContainers reference implementation.
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/seedwork-domain-model-base-classes-interfaces
/// </remarks>
public abstract class Entity
{
    private int? _requestedHashCode;
    private List<INotification>? _domainEvents;

    /// <summary>
    /// Unique identifier for the entity.
    /// Using int for simplicity — can change to Guid if needed.
    /// </summary>
    public virtual int Id { get; protected set; }

    /// <summary>
    /// Domain events raised by this entity.
    /// Will be dispatched after the entity is persisted.
    /// </summary>
    public IReadOnlyCollection<INotification>? DomainEvents => _domainEvents?.AsReadOnly();

    /// <summary>
    /// Adds a domain event to be dispatched after persistence.
    /// </summary>
    public void AddDomainEvent(INotification eventItem)
    {
        _domainEvents ??= [];
        _domainEvents.Add(eventItem);
    }

    /// <summary>
    /// Removes a domain event.
    /// </summary>
    public void RemoveDomainEvent(INotification eventItem)
    {
        _domainEvents?.Remove(eventItem);
    }

    /// <summary>
    /// Clears all domain events. Called after events are dispatched.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents?.Clear();
    }

    /// <summary>
    /// Returns true if the entity has not been persisted yet.
    /// </summary>
    public bool IsTransient() => Id == default;

    /// <summary>
    /// Equality is based on ID, not reference.
    /// Two entities with the same ID are considered equal.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity entity)
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        if (GetType() != entity.GetType())
            return false;

        if (entity.IsTransient() || IsTransient())
            return false;

        return entity.Id == Id;
    }

    /// <summary>
    /// Hash code is based on ID for consistency with Equals.
    /// </summary>
    public override int GetHashCode()
    {
        if (IsTransient())
            return base.GetHashCode();

        _requestedHashCode ??= Id.GetHashCode() ^ 31;
        return _requestedHashCode.Value;
    }

    public static bool operator ==(Entity? left, Entity? right)
    {
        if (left is null)
            return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(Entity? left, Entity? right)
    {
        return !(left == right);
    }
}
```

### .NET 10 Modern Features Used:

| Feature | Usage |
|---------|-------|
| **File-scoped namespace** | `namespace Nastart.Domain.Common;` |
| **Collection expressions** | `_domainEvents ??= [];` |
| **Nullable reference types** | `List<INotification>?` |
| **Pattern matching** | `obj is not Entity entity` |
| **Target-typed new** | Implicit type on `[]` |

### Install MediatR Package:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Domain
dotnet add package MediatR.Contracts
```

> ⚠️ **Note**: We only add `MediatR.Contracts` to the Domain layer (just the interfaces). The full `MediatR` package goes in the Application layer.

### Your Task (Day 2):

1. Create `Entity.cs` in `Common/` folder
2. Install `MediatR.Contracts` package
3. Verify build: `dotnet build`

---

# Day 3: Value Objects

## 🧒 Explain Like I'm 5

Think about money. A $10 bill in your pocket is the SAME as a $10 bill in my pocket — they're both worth $10! We don't care WHICH specific bill it is.

**Value Objects** are things where we only care about the VALUE, not which specific one:
- `$10.00` equals `$10.00` (same amount = same value)
- `500 grams` equals `500 grams`
- `"Indonesian Rupiah"` equals `"Indonesian Rupiah"`

## 🔧 Engineer Language

A **Value Object** is:
1. **Defined by its attributes** — No separate identity
2. **Immutable** — Cannot be changed after creation
3. **Equality by value** — Two value objects are equal if all properties match

> 📖 **Microsoft Docs**: *"A value object is an object with no conceptual identity that describes a domain aspect. These are objects that you instantiate to represent design elements that only concern you temporarily. You care about what they are, not who they are."*
>
> — [Design a microservice domain model](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model)

### Two Approaches in .NET 10:

| Approach | Best For | Pros | Cons |
|----------|----------|------|------|
| **C# record** | Simple value objects | Built-in equality, immutable, concise | Less control |
| **ValueObject base class** | Complex value objects | Full control, validation | More code |

### Approach 1: Using C# Records (Recommended for Simple Cases)

```csharp
// Simple value objects as records — .NET 10 idiomatic approach
namespace Nastart.Domain.Inventory.ValueObjects;

/// <summary>
/// Represents a monetary amount with currency.
/// Immutable value object using C# record.
/// </summary>
public sealed record Money(decimal Amount, string Currency = "IDR")
{
    public static Money Zero => new(0);
    public static Money FromIDR(decimal amount) => new(amount, "IDR");

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot add {Currency} to {other.Currency}");
        
        return this with { Amount = Amount + other.Amount };
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot subtract {other.Currency} from {Currency}");
        
        return this with { Amount = Amount - other.Amount };
    }

    public Money Multiply(decimal factor) => this with { Amount = Amount * factor };

    public bool IsZero => Amount == 0;
    public bool IsNegative => Amount < 0;

    public override string ToString() => $"{Currency} {Amount:N0}";
}

/// <summary>
/// Represents a quantity with a unit of measurement.
/// </summary>
public sealed record Quantity(decimal Value, string Unit)
{
    public static Quantity Zero(string unit) => new(0, unit);
    public static Quantity Kilograms(decimal value) => new(value, "kg");
    public static Quantity Grams(decimal value) => new(value, "g");
    public static Quantity Liters(decimal value) => new(value, "L");
    public static Quantity Milliliters(decimal value) => new(value, "mL");
    public static Quantity Pieces(decimal value) => new(value, "pcs");

    public Quantity Add(Quantity other)
    {
        if (Unit != other.Unit)
            throw new InvalidOperationException($"Cannot add {Unit} to {other.Unit}");
        
        return this with { Value = Value + other.Value };
    }

    public Quantity Subtract(Quantity other)
    {
        if (Unit != other.Unit)
            throw new InvalidOperationException($"Cannot subtract {other.Unit} from {Unit}");
        
        return this with { Value = Value - other.Value };
    }

    public bool IsEmpty => Value <= 0;

    public override string ToString() => $"{Value:N2} {Unit}";
}

/// <summary>
/// Represents a percentage value (0-100).
/// </summary>
public sealed record Percentage(decimal Value)
{
    public static Percentage Zero => new(0);
    public static Percentage FromDecimal(decimal decimalValue) => new(decimalValue * 100);

    public bool IsBelow(decimal threshold) => Value < threshold;
    public bool IsAbove(decimal threshold) => Value > threshold;
    public decimal ToDecimal() => Value / 100;

    public override string ToString() => $"{Value:N1}%";
}
```

### Approach 2: ValueObject Base Class (For Complex Cases)

Create `src/Nastart.Domain/Common/ValueObject.cs`:

```csharp
namespace Nastart.Domain.Common;

/// <summary>
/// Base class for value objects that need complex equality logic.
/// Use C# records for simple value objects instead.
/// </summary>
/// <remarks>
/// Based on Microsoft's eShopOnContainers implementation.
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/implement-value-objects
/// </remarks>
public abstract class ValueObject
{
    /// <summary>
    /// Returns the components used for equality comparison.
    /// Override in derived classes to specify which properties to compare.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
            return false;

        var other = (ValueObject)obj;
        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents()
            .Select(x => x?.GetHashCode() ?? 0)
            .Aggregate((x, y) => x ^ y);
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        if (left is null)
            return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !(left == right);
    }
}
```

### Example Using ValueObject Base Class:

```csharp
using Nastart.Domain.Common;

namespace Nastart.Domain.Inventory.ValueObjects;

/// <summary>
/// Represents a physical address.
/// Uses ValueObject base class for complex equality.
/// </summary>
public class Address : ValueObject
{
    public string Street { get; private set; }
    public string City { get; private set; }
    public string Province { get; private set; }
    public string PostalCode { get; private set; }

    // Private constructor for EF Core
    private Address() 
    { 
        Street = string.Empty;
        City = string.Empty;
        Province = string.Empty;
        PostalCode = string.Empty;
    }

    public Address(string street, string city, string province, string postalCode)
    {
        Street = street ?? throw new ArgumentNullException(nameof(street));
        City = city ?? throw new ArgumentNullException(nameof(city));
        Province = province ?? throw new ArgumentNullException(nameof(province));
        PostalCode = postalCode ?? throw new ArgumentNullException(nameof(postalCode));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return Province;
        yield return PostalCode;
    }

    public override string ToString() => $"{Street}, {City}, {Province} {PostalCode}";
}
```

### Strongly-Typed IDs (Modern Pattern):

```csharp
namespace Nastart.Domain.Inventory.ValueObjects;

/// <summary>
/// Strongly-typed ID for Ingredient entity.
/// Prevents mixing up IDs from different entities.
/// </summary>
public readonly record struct IngredientId(Guid Value)
{
    public static IngredientId New() => new(Guid.NewGuid());
    public static IngredientId Empty => new(Guid.Empty);
    
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed ID for Recipe entity.
/// </summary>
public readonly record struct RecipeId(Guid Value)
{
    public static RecipeId New() => new(Guid.NewGuid());
    public static RecipeId Empty => new(Guid.Empty);
    
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed ID for Purchase entity.
/// </summary>
public readonly record struct PurchaseId(Guid Value)
{
    public static PurchaseId New() => new(Guid.NewGuid());
    public static PurchaseId Empty => new(Guid.Empty);
    
    public override string ToString() => Value.ToString();
}
```

### Your Task (Day 3):

1. Create `ValueObject.cs` in `Common/` folder
2. Create `Money.cs`, `Quantity.cs`, `Percentage.cs` in `Inventory/ValueObjects/`
3. Create `IngredientId.cs`, `RecipeId.cs`, `PurchaseId.cs` for strongly-typed IDs
4. Verify build: `dotnet build`

---

# Day 4: Domain Events

## 🧒 Explain Like I'm 5

Imagine you're at a birthday party. When the cake arrives, someone SHOUTS:

> "🎂 THE CAKE IS HERE!"

Everyone hears it! Some people come to take photos, some get plates ready, some start singing. They all REACT to the announcement.

**Domain Events** are those announcements:
- `PriceChangedEvent` — "The flour price changed!"
- `LowStockEvent` — "We're running low on sugar!"
- `MarginBelowThresholdEvent` — "Warning! Cookies profit is too low!"

Different parts of the app HEAR these announcements and react.

## 🔧 Engineer Language

A **Domain Event** is:
1. **Something that happened** — Past tense naming (`OrderPlaced`, not `PlaceOrder`)
2. **Immutable** — Cannot be changed after creation
3. **Published after state change** — Raised when aggregate state changes
4. **Handled by event handlers** — Other parts of system react to it

> 📖 **Microsoft Docs**: *"Use domain events to explicitly implement side effects of changes within your domain. In other words, and using DDD terminology, use domain events to explicitly implement side effects across multiple aggregates."*
>
> — [Domain events: Design and implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)

### Domain Event Interface:

Create `src/Nastart.Domain/Common/IDomainEvent.cs`:

```csharp
using MediatR;

namespace Nastart.Domain.Common;

/// <summary>
/// Marker interface for domain events.
/// Implements INotification for MediatR dispatching.
/// </summary>
/// <remarks>
/// Domain events are dispatched after the aggregate is persisted,
/// ensuring eventual consistency.
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation
/// </remarks>
public interface IDomainEvent : INotification
{
    /// <summary>
    /// When the event occurred.
    /// </summary>
    DateTime OccurredOn { get; }
}
```

### Base Domain Event:

```csharp
namespace Nastart.Domain.Common;

/// <summary>
/// Base class for all domain events with common properties.
/// </summary>
public abstract record DomainEventBase : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
```

### Nastart Domain Events:

Create `src/Nastart.Domain/Inventory/Events/PriceChangedEvent.cs`:

```csharp
using Nastart.Domain.Common;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Domain.Inventory.Events;

/// <summary>
/// Raised when an ingredient's price changes.
/// Triggers recipe cost recalculation.
/// </summary>
public sealed record PriceChangedEvent(
    IngredientId IngredientId,
    string IngredientName,
    Money OldPrice,
    Money NewPrice,
    decimal PercentageChange
) : DomainEventBase
{
    /// <summary>
    /// Returns true if this is a significant price increase (>15%).
    /// </summary>
    public bool IsPriceSpike => PercentageChange > 15;
}
```

Create `src/Nastart.Domain/Inventory/Events/PriceSpikeDetectedEvent.cs`:

```csharp
using Nastart.Domain.Common;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Domain.Inventory.Events;

/// <summary>
/// Raised when a price spike is detected (>15% increase).
/// Triggers alert creation and notification.
/// </summary>
public sealed record PriceSpikeDetectedEvent(
    IngredientId IngredientId,
    string IngredientName,
    Money OldPrice,
    Money NewPrice,
    decimal PercentageIncrease
) : DomainEventBase;
```

Create `src/Nastart.Domain/Inventory/Events/LowStockEvent.cs`:

```csharp
using Nastart.Domain.Common;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Domain.Inventory.Events;

/// <summary>
/// Raised when ingredient stock falls below minimum threshold.
/// </summary>
public sealed record LowStockEvent(
    IngredientId IngredientId,
    string IngredientName,
    Quantity CurrentStock,
    Quantity MinimumStock
) : DomainEventBase;
```

### How Events Flow:

```
┌─────────────────┐     ┌────────────────┐     ┌─────────────────┐
│  Ingredient     │     │  MediatR       │     │  Event Handlers │
│  Aggregate      │────▶│  Dispatcher    │────▶│                 │
│                 │     │                │     │ • UpdateRecipes │
│ AddDomainEvent( │     │ Publish(event) │     │ • SendAlert     │
│   PriceChanged) │     │                │     │ • LogHistory    │
└─────────────────┘     └────────────────┘     └─────────────────┘
```

### Your Task (Day 4):

1. Create `IDomainEvent.cs` and `DomainEventBase.cs` in `Common/`
2. Create `PriceChangedEvent.cs`, `PriceSpikeDetectedEvent.cs`, `LowStockEvent.cs`
3. Verify build: `dotnet build`

---

# Day 5: Aggregate Root Pattern

## 🧒 Explain Like I'm 5

Imagine a **treasure chest** full of gold coins and jewels:
- The **chest** is the boss (aggregate root)
- You can only ADD or REMOVE treasure through the chest
- You can't reach in and secretly take a coin — the chest controls everything!

In our app:
- `Ingredient` is a treasure chest
- It controls its own price, stock, and history
- Other parts of the app can't change things directly — they ASK the Ingredient to do it

## 🔧 Engineer Language

An **Aggregate Root** is:
1. **Entry point** — Only way to access child entities
2. **Consistency guardian** — Ensures all invariants are satisfied
3. **Transaction boundary** — One aggregate = one transaction
4. **Event publisher** — Raises domain events for state changes

> 📖 **Microsoft Docs**: *"The purpose of an aggregate root is to ensure the consistency of the aggregate; it should be the only entry point for updates to the aggregate through methods or operations in the aggregate root class."*
>
> — [Design a microservice domain model](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model#the-domain-entity-pattern)

### Aggregate Root Interface:

Create `src/Nastart.Domain/Common/IAggregateRoot.cs`:

```csharp
namespace Nastart.Domain.Common;

/// <summary>
/// Marker interface to identify aggregate roots.
/// Only aggregate roots should have repositories.
/// </summary>
/// <remarks>
/// This is a marker interface — it has no methods.
/// It's used to constrain the repository generic type.
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design
/// </remarks>
public interface IAggregateRoot
{
}
```

### Aggregate Root Base Class:

Create `src/Nastart.Domain/Common/AggregateRoot.cs`:

```csharp
namespace Nastart.Domain.Common;

/// <summary>
/// Base class for aggregate roots.
/// Combines Entity functionality with aggregate root marker.
/// </summary>
public abstract class AggregateRoot : Entity, IAggregateRoot
{
    /// <summary>
    /// Version for optimistic concurrency.
    /// Incremented on each modification.
    /// </summary>
    public int Version { get; protected set; }

    /// <summary>
    /// Increments version for optimistic concurrency control.
    /// Call this in methods that modify state.
    /// </summary>
    protected void IncrementVersion()
    {
        Version++;
    }
}
```

### Repository Interface:

Create `src/Nastart.Domain/Common/IRepository.cs`:

```csharp
namespace Nastart.Domain.Common;

/// <summary>
/// Generic repository interface for aggregate roots.
/// Constrained to IAggregateRoot to enforce DDD rules.
/// </summary>
/// <remarks>
/// Repositories are only for aggregate roots!
/// Child entities are accessed through their aggregate root.
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design
/// </remarks>
public interface IRepository<T> where T : IAggregateRoot
{
    /// <summary>
    /// Unit of work for transaction management.
    /// </summary>
    IUnitOfWork UnitOfWork { get; }
}
```

### Unit of Work Interface:

Create `src/Nastart.Domain/Common/IUnitOfWork.cs`:

```csharp
namespace Nastart.Domain.Common;

/// <summary>
/// Unit of Work pattern for coordinating persistence.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Saves all changes made in this unit of work.
    /// Returns the number of entities written to the database.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves changes and dispatches domain events.
    /// </summary>
    Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default);
}
```

### Your Task (Day 5):

1. Create `IAggregateRoot.cs`, `AggregateRoot.cs` in `Common/`
2. Create `IRepository.cs`, `IUnitOfWork.cs` in `Common/`
3. Verify build: `dotnet build`

---

# Day 6: Building the Ingredient Aggregate

## 🧒 Explain Like I'm 5

Now let's build our first REAL treasure chest — the **Ingredient**!

An Ingredient knows:
- Its **name** (like "Flour" or "Sugar")
- Its **price** (how much it costs per kg)
- Its **stock** (how much we have)
- When the price **changed** (so we can track it)

And it can:
- **Update its price** (and tell everyone the price changed!)
- **Check if stock is low** (and warn us!)
- **Calculate if there's a price spike** (and alert us!)

## 🔧 Engineer Language

The `Ingredient` aggregate is the central entity of the Inventory bounded context. It encapsulates:
- Price management with spike detection
- Stock tracking with low-stock alerts
- Domain event publishing

### The Ingredient Aggregate:

Create `src/Nastart.Domain/Inventory/Aggregates/Ingredient.cs`:

```csharp
using Nastart.Domain.Common;
using Nastart.Domain.Inventory.Events;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Domain.Inventory.Aggregates;

/// <summary>
/// Aggregate root representing an ingredient tracked in inventory.
/// </summary>
/// <remarks>
/// Following DDD principles:
/// - All modifications go through methods (not property setters)
/// - Business rules are encapsulated in the aggregate
/// - Domain events are raised for significant state changes
/// 
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/net-core-microservice-domain-model
/// </remarks>
public class Ingredient : AggregateRoot
{
    // Constants for business rules
    private const decimal PriceSpikeThreshold = 0.15m; // 15%
    
    // Private fields for encapsulation (EF Core can access these)
    private string _name = string.Empty;
    private string _unit = string.Empty;
    private decimal _currentPriceAmount;
    private string _currentPriceCurrency = "IDR";
    private decimal _stockQuantity;
    private decimal _minimumStock;
    private int? _categoryId;

    // Protected constructor for EF Core
    protected Ingredient() { }

    /// <summary>
    /// Creates a new Ingredient with initial values.
    /// </summary>
    public Ingredient(
        string name,
        string unit,
        Money initialPrice,
        Quantity minimumStock,
        int? categoryId = null)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Ingredient name is required", nameof(name));
        if (string.IsNullOrWhiteSpace(unit))
            throw new ArgumentException("Unit is required", nameof(unit));
        if (initialPrice.Amount < 0)
            throw new ArgumentException("Price cannot be negative", nameof(initialPrice));

        _name = name;
        _unit = unit;
        _currentPriceAmount = initialPrice.Amount;
        _currentPriceCurrency = initialPrice.Currency;
        _stockQuantity = 0;
        _minimumStock = minimumStock.Value;
        _categoryId = categoryId;
        
        IncrementVersion();
    }

    // Public read-only properties
    public string Name => _name;
    public string Unit => _unit;
    public Money CurrentPrice => new(_currentPriceAmount, _currentPriceCurrency);
    public Quantity Stock => new(_stockQuantity, _unit);
    public Quantity MinimumStock => new(_minimumStock, _unit);
    public int? CategoryId => _categoryId;

    /// <summary>
    /// Returns true if current stock is below minimum threshold.
    /// </summary>
    public bool IsLowStock => _stockQuantity < _minimumStock;

    /// <summary>
    /// Updates the ingredient's price from a new purchase.
    /// Detects price spikes and raises appropriate events.
    /// </summary>
    public void UpdatePrice(Money newPrice)
    {
        if (newPrice.Amount < 0)
            throw new ArgumentException("Price cannot be negative", nameof(newPrice));

        var oldPrice = CurrentPrice;
        
        // Skip if price hasn't changed
        if (oldPrice.Amount == newPrice.Amount && oldPrice.Currency == newPrice.Currency)
            return;

        // Calculate percentage change
        decimal percentageChange = 0;
        if (oldPrice.Amount > 0)
        {
            percentageChange = ((newPrice.Amount - oldPrice.Amount) / oldPrice.Amount) * 100;
        }

        // Update the price
        _currentPriceAmount = newPrice.Amount;
        _currentPriceCurrency = newPrice.Currency;
        IncrementVersion();

        // Raise PriceChanged event
        AddDomainEvent(new PriceChangedEvent(
            IngredientId: new IngredientId(Guid.Empty), // Will be set after persistence
            IngredientName: _name,
            OldPrice: oldPrice,
            NewPrice: newPrice,
            PercentageChange: percentageChange
        ));

        // Check for price spike (>15% increase)
        if (percentageChange > PriceSpikeThreshold * 100)
        {
            AddDomainEvent(new PriceSpikeDetectedEvent(
                IngredientId: new IngredientId(Guid.Empty),
                IngredientName: _name,
                OldPrice: oldPrice,
                NewPrice: newPrice,
                PercentageIncrease: percentageChange
            ));
        }
    }

    /// <summary>
    /// Adds stock to the ingredient (from a purchase).
    /// </summary>
    public void AddStock(Quantity quantity)
    {
        if (quantity.Unit != _unit)
            throw new InvalidOperationException($"Cannot add {quantity.Unit} to ingredient measured in {_unit}");
        
        if (quantity.Value < 0)
            throw new ArgumentException("Quantity cannot be negative", nameof(quantity));

        _stockQuantity += quantity.Value;
        IncrementVersion();
    }

    /// <summary>
    /// Removes stock from the ingredient (used in recipe production).
    /// </summary>
    public void RemoveStock(Quantity quantity)
    {
        if (quantity.Unit != _unit)
            throw new InvalidOperationException($"Cannot remove {quantity.Unit} from ingredient measured in {_unit}");
        
        if (quantity.Value < 0)
            throw new ArgumentException("Quantity cannot be negative", nameof(quantity));

        if (_stockQuantity < quantity.Value)
            throw new InvalidOperationException($"Insufficient stock. Available: {_stockQuantity} {_unit}, Requested: {quantity.Value} {_unit}");

        _stockQuantity -= quantity.Value;
        IncrementVersion();

        // Check if stock is now low
        if (IsLowStock)
        {
            AddDomainEvent(new LowStockEvent(
                IngredientId: new IngredientId(Guid.Empty),
                IngredientName: _name,
                CurrentStock: Stock,
                MinimumStock: MinimumStock
            ));
        }
    }

    /// <summary>
    /// Updates the minimum stock threshold.
    /// </summary>
    public void SetMinimumStock(Quantity minimumStock)
    {
        if (minimumStock.Unit != _unit)
            throw new InvalidOperationException($"Minimum stock unit must be {_unit}");
        
        _minimumStock = minimumStock.Value;
        IncrementVersion();
    }

    /// <summary>
    /// Updates the ingredient's category.
    /// </summary>
    public void SetCategory(int categoryId)
    {
        _categoryId = categoryId;
        IncrementVersion();
    }

    /// <summary>
    /// Renames the ingredient.
    /// </summary>
    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name cannot be empty", nameof(newName));
        
        _name = newName;
        IncrementVersion();
    }
}
```

### Key DDD Principles Applied:

| Principle | Implementation |
|-----------|----------------|
| **Encapsulation** | Private fields, public methods |
| **Invariants** | Validation in constructor and methods |
| **Rich Domain Model** | Business logic in entity, not services |
| **Domain Events** | Events raised on state changes |
| **Immutable Value Objects** | `Money`, `Quantity` as records |

### What NOT to Do (Anti-patterns):

```csharp
// ❌ WRONG — Anemic Domain Model
public class IngredientBad
{
    public string Name { get; set; }  // Public setter!
    public decimal Price { get; set; } // No encapsulation!
}

// ❌ WRONG — Logic in Service
public class IngredientService
{
    public void UpdatePrice(Ingredient ingredient, decimal newPrice)
    {
        // Business logic outside the entity = anemic model!
        ingredient.Price = newPrice;
    }
}

// ✅ CORRECT — Rich Domain Model
ingredient.UpdatePrice(new Money(15000, "IDR"));
// Logic is INSIDE the aggregate, where it belongs!
```

### Your Task (Day 6):

1. Create `Ingredient.cs` in `Inventory/Aggregates/`
2. Ensure all Value Objects are created
3. Verify build: `dotnet build`

---

# Day 7: Unit Testing Your Domain

## 🧒 Explain Like I'm 5

Before you let people ride a new roller coaster, you TEST it first! You make sure:
- It goes where it should
- It doesn't break
- It's safe

We do the same with our code:
- Does `UpdatePrice` actually change the price?
- Does it detect price spikes correctly?
- Does it throw errors for bad data?

## 🔧 Engineer Language

Unit tests verify that your domain logic works correctly in isolation. We test:
1. **Construction** — Can we create valid entities?
2. **Business rules** — Do methods enforce invariants?
3. **Domain events** — Are correct events raised?
4. **Edge cases** — Do errors occur for invalid input?

### Set Up Test Project:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend

# Add reference to Domain project
dotnet add tests/Nastart.Domain.Tests/Nastart.Domain.Tests.csproj reference src/Nastart.Domain/Nastart.Domain.csproj

# Install FluentAssertions for readable tests
dotnet add tests/Nastart.Domain.Tests package FluentAssertions
```

### Ingredient Tests:

Create `tests/Nastart.Domain.Tests/Inventory/IngredientTests.cs`:

```csharp
using FluentAssertions;
using Nastart.Domain.Inventory.Aggregates;
using Nastart.Domain.Inventory.Events;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Domain.Tests.Inventory;

public class IngredientTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesIngredient()
    {
        // Arrange
        var name = "Flour";
        var unit = "kg";
        var price = new Money(15000, "IDR");
        var minStock = new Quantity(5, "kg");

        // Act
        var ingredient = new Ingredient(name, unit, price, minStock);

        // Assert
        ingredient.Name.Should().Be(name);
        ingredient.Unit.Should().Be(unit);
        ingredient.CurrentPrice.Should().Be(price);
        ingredient.MinimumStock.Should().Be(minStock);
        ingredient.Stock.Value.Should().Be(0);
        ingredient.IsLowStock.Should().BeTrue(); // 0 < 5
    }

    [Fact]
    public void Constructor_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange & Act
        var act = () => new Ingredient(
            name: "",
            unit: "kg",
            initialPrice: new Money(15000),
            minimumStock: new Quantity(5, "kg")
        );

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithMessage("*name*");
    }

    [Fact]
    public void UpdatePrice_WithHigherPrice_RaisesPriceChangedEvent()
    {
        // Arrange
        var ingredient = CreateTestIngredient(initialPrice: 10000);
        var newPrice = new Money(12000, "IDR");

        // Act
        ingredient.UpdatePrice(newPrice);

        // Assert
        ingredient.CurrentPrice.Should().Be(newPrice);
        ingredient.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PriceChangedEvent>();
    }

    [Fact]
    public void UpdatePrice_WithPriceSpike_RaisesPriceSpikeDetectedEvent()
    {
        // Arrange
        var ingredient = CreateTestIngredient(initialPrice: 10000);
        var newPrice = new Money(15000, "IDR"); // 50% increase

        // Act
        ingredient.UpdatePrice(newPrice);

        // Assert
        ingredient.DomainEvents.Should().HaveCount(2);
        ingredient.DomainEvents.Should().ContainSingle(e => e is PriceChangedEvent);
        ingredient.DomainEvents.Should().ContainSingle(e => e is PriceSpikeDetectedEvent);
    }

    [Fact]
    public void UpdatePrice_WithSamePrice_DoesNotRaiseEvent()
    {
        // Arrange
        var ingredient = CreateTestIngredient(initialPrice: 10000);
        var samePrice = new Money(10000, "IDR");

        // Act
        ingredient.UpdatePrice(samePrice);

        // Assert
        ingredient.DomainEvents.Should().BeNullOrEmpty();
    }

    [Fact]
    public void AddStock_WithValidQuantity_IncreasesStock()
    {
        // Arrange
        var ingredient = CreateTestIngredient();
        var quantity = new Quantity(10, "kg");

        // Act
        ingredient.AddStock(quantity);

        // Assert
        ingredient.Stock.Value.Should().Be(10);
        ingredient.IsLowStock.Should().BeFalse(); // 10 > 5
    }

    [Fact]
    public void AddStock_WithWrongUnit_ThrowsInvalidOperationException()
    {
        // Arrange
        var ingredient = CreateTestIngredient(); // unit = "kg"
        var quantity = new Quantity(10, "L"); // wrong unit

        // Act
        var act = () => ingredient.AddStock(quantity);

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Cannot add*");
    }

    [Fact]
    public void RemoveStock_WhenBelowMinimum_RaisesLowStockEvent()
    {
        // Arrange
        var ingredient = CreateTestIngredient();
        ingredient.AddStock(new Quantity(10, "kg")); // Stock = 10, Min = 5
        
        // Act
        ingredient.RemoveStock(new Quantity(8, "kg")); // Stock = 2, below min

        // Assert
        ingredient.IsLowStock.Should().BeTrue();
        ingredient.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<LowStockEvent>();
    }

    [Fact]
    public void RemoveStock_WithInsufficientStock_ThrowsInvalidOperationException()
    {
        // Arrange
        var ingredient = CreateTestIngredient();
        ingredient.AddStock(new Quantity(5, "kg"));

        // Act
        var act = () => ingredient.RemoveStock(new Quantity(10, "kg"));

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*Insufficient stock*");
    }

    // Helper method to reduce repetition
    private static Ingredient CreateTestIngredient(decimal initialPrice = 15000)
    {
        return new Ingredient(
            name: "Test Flour",
            unit: "kg",
            initialPrice: new Money(initialPrice, "IDR"),
            minimumStock: new Quantity(5, "kg")
        );
    }
}
```

### Value Object Tests:

Create `tests/Nastart.Domain.Tests/ValueObjects/MoneyTests.cs`:

```csharp
using FluentAssertions;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void TwoMoneyWithSameValue_AreEqual()
    {
        // Arrange
        var money1 = new Money(10000, "IDR");
        var money2 = new Money(10000, "IDR");

        // Assert
        money1.Should().Be(money2);
        (money1 == money2).Should().BeTrue();
    }

    [Fact]
    public void Add_WithSameCurrency_ReturnsSum()
    {
        // Arrange
        var money1 = new Money(10000, "IDR");
        var money2 = new Money(5000, "IDR");

        // Act
        var result = money1.Add(money2);

        // Assert
        result.Amount.Should().Be(15000);
    }

    [Fact]
    public void Add_WithDifferentCurrency_ThrowsException()
    {
        // Arrange
        var idr = new Money(10000, "IDR");
        var usd = new Money(10, "USD");

        // Act
        var act = () => idr.Add(usd);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Multiply_ReturnsCorrectAmount()
    {
        // Arrange
        var money = new Money(1000, "IDR");

        // Act
        var result = money.Multiply(2.5m);

        // Assert
        result.Amount.Should().Be(2500);
    }
}
```

### Run Tests:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend

# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~IngredientTests"
```

### Your Task (Day 7):

1. Set up test project with FluentAssertions
2. Create `IngredientTests.cs` 
3. Create `MoneyTests.cs`
4. Run `dotnet test` — all tests should pass!

---

# Resources

> ✅ All links verified as of January 2026 using Microsoft Learn

## Domain Layer Fundamentals (Microsoft Official)

| Resource | Type | Link |
|----------|------|------|
| **Implement a microservice domain model with .NET** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/net-core-microservice-domain-model |
| **Seedwork base classes and interfaces** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/seedwork-domain-model-base-classes-interfaces |
| **Implement value objects** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/implement-value-objects |
| **Domain events: Design and implementation** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation |
| **Design a microservice domain model** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model |

## Tactical DDD Patterns

| Resource | Type | Link |
|----------|------|------|
| **Using tactical DDD to design microservices** | MS Docs | https://learn.microsoft.com/en-us/azure/architecture/microservices/model/tactical-ddd |
| **Design a DDD-oriented microservice** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice |
| **Apply simplified CQRS and DDD patterns** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/apply-simplified-microservice-cqrs-ddd-patterns |

## C# Language Features (for Value Objects)

| Resource | Type | Link |
|----------|------|------|
| **Records (C# reference)** | MS Docs | https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record |
| **Introduction to record types in C#** | MS Docs | https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records |
| **How to define value equality** | MS Docs | https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/statements-expressions-operators/how-to-define-value-equality-for-a-type |
| **Struct Design guidelines** | MS Docs | https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/struct |

## Reference Implementation

| Resource | Type | Link |
|----------|------|------|
| **eShopOnContainers (GitHub)** | Code | https://github.com/dotnet/eShop |
| **eShop Ordering.Domain** | Code | https://github.com/dotnet/eShop/tree/main/src/Ordering.Domain |
| **MediatR Library** | GitHub | https://github.com/jbogard/MediatR |

## Unit Testing

| Resource | Type | Link |
|----------|------|------|
| **Unit testing C# with xUnit** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-xunit |
| **Best practices for unit testing** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices |
| **FluentAssertions Documentation** | Docs | https://fluentassertions.com/introduction |

## eBooks (Free from Microsoft)

| Resource | Description | Link |
|----------|-------------|------|
| **.NET Microservices Architecture** | Complete DDD/CQRS guide | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/ |
| **Architect Modern Web Apps** | Clean Architecture patterns | https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/ |

---

# Week 2 Checklist

- [ ] **Day 1**: Create SeedWork folder structure
  - [ ] Create `Common/` folder in Domain project
  - [ ] Create `Inventory/Aggregates/`, `Inventory/ValueObjects/`, `Inventory/Events/` folders
  - [ ] Remove `Class1.cs` placeholder

- [ ] **Day 2**: Entity Base Class
  - [ ] Install `MediatR.Contracts` package
  - [ ] Create `Entity.cs` with ID, equality, and domain events
  - [ ] Verify build passes

- [ ] **Day 3**: Value Objects
  - [ ] Create `ValueObject.cs` base class
  - [ ] Create `Money.cs` record
  - [ ] Create `Quantity.cs` record
  - [ ] Create `Percentage.cs` record
  - [ ] Create strongly-typed IDs (`IngredientId`, `RecipeId`, `PurchaseId`)

- [ ] **Day 4**: Domain Events
  - [ ] Create `IDomainEvent.cs` interface
  - [ ] Create `DomainEventBase.cs` record
  - [ ] Create `PriceChangedEvent.cs`
  - [ ] Create `PriceSpikeDetectedEvent.cs`
  - [ ] Create `LowStockEvent.cs`

- [ ] **Day 5**: Aggregate Root
  - [ ] Create `IAggregateRoot.cs` interface
  - [ ] Create `AggregateRoot.cs` base class
  - [ ] Create `IRepository.cs` interface
  - [ ] Create `IUnitOfWork.cs` interface

- [ ] **Day 6**: Ingredient Aggregate
  - [ ] Create `Ingredient.cs` with all business logic
  - [ ] Implement `UpdatePrice()`, `AddStock()`, `RemoveStock()`
  - [ ] Verify domain events are raised correctly

- [ ] **Day 7**: Unit Tests
  - [ ] Add project reference and FluentAssertions
  - [ ] Create `IngredientTests.cs`
  - [ ] Create `MoneyTests.cs`
  - [ ] All tests pass with `dotnet test`

---

## File Summary

After completing Week 2, your Domain project should have:

```
src/Nastart.Domain/
├── Common/
│   ├── Entity.cs
│   ├── AggregateRoot.cs
│   ├── IAggregateRoot.cs
│   ├── ValueObject.cs
│   ├── IDomainEvent.cs
│   ├── DomainEventBase.cs
│   ├── IRepository.cs
│   └── IUnitOfWork.cs
├── Inventory/
│   ├── Aggregates/
│   │   └── Ingredient.cs
│   ├── ValueObjects/
│   │   ├── Money.cs
│   │   ├── Quantity.cs
│   │   ├── Percentage.cs
│   │   ├── IngredientId.cs
│   │   ├── RecipeId.cs
│   │   └── PurchaseId.cs
│   └── Events/
│       ├── PriceChangedEvent.cs
│       ├── PriceSpikeDetectedEvent.cs
│       └── LowStockEvent.cs
└── Nastart.Domain.csproj
```

---

**Next Week**: [Week 3 - Finance Context Domain](./week-03-finance-domain.md)

---

*Remember: The Domain layer is the HEART of your application. Take time to get it right!* 💎
