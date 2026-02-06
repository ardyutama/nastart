# Week 2: Feature Building Blocks 🏗️

> **Goal**: Build reusable building blocks for your features — Models, DTOs, Result pattern, MediatR notifications, and your first real feature slice: `CreateIngredient`.

---

## Table of Contents
1. [Day 1: Feature Folder Structure](#day-1-feature-folder-structure)
2. [Day 2: Models & Database Entities](#day-2-models--database-entities)
3. [Day 3: C# Records for Feature Data](#day-3-c-records-for-feature-data)
4. [Day 4: MediatR Notifications for Side Effects](#day-4-mediatr-notifications-for-side-effects)
5. [Day 5: The Feature Slice Pattern](#day-5-the-feature-slice-pattern)
6. [Day 6: Building the CreateIngredient Feature](#day-6-building-the-createingredient-feature)
7. [Day 7: Testing Your Feature Slices](#day-7-testing-your-feature-slices)
8. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1: Feature Folder Structure

## 🧒 Explain Like I'm 5

Imagine you're organizing your toys. Instead of putting all dolls in one box, all cars in another, and all accessories in a third (making it hard to find everything for playtime), you put everything for "Playing Store" in one box — the cash register, play money, toy food, all together!

In **Vertical Slice Architecture**, we organize code the same way — everything for one feature lives together:
- The request (what the user wants)
- The handler (the code that does it)
- The response (what we send back)

## 🔧 Engineer Language

**Vertical Slice Architecture** organizes code by feature, not by layer. Each "slice" contains everything needed to handle one user request — from the API endpoint down to the database query.

> 📖 **Microsoft Docs**: *"Minimal APIs are designed to create HTTP APIs with minimal dependencies. They're ideal for microservices and apps that want to include only the minimum files, features, and dependencies in ASP.NET Core."*
>
> — [Minimal APIs overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview)

### Feature-Based Folder Structure:

```
src/Nastart.Api/
├── Features/
│   ├── Ingredients/
│   │   ├── CreateIngredient.cs          # Command + Handler + Endpoint
│   │   ├── GetIngredient.cs             # Query + Handler + Endpoint
│   │   ├── GetIngredients.cs            # Query + Handler + Endpoint
│   │   ├── UpdateIngredientPrice.cs     # Command + Handler + Endpoint
│   │   ├── Ingredient.cs                # Entity model
│   │   └── IngredientsEndpoints.cs      # Route group registration
│   │
│   ├── Recipes/
│   │   ├── CreateRecipe.cs
│   │   ├── GetRecipeCost.cs
│   │   ├── Recipe.cs
│   │   └── RecipesEndpoints.cs
│   │
│   └── Purchases/
│       ├── RecordPurchase.cs
│       ├── ScanReceipt.cs
│       ├── Purchase.cs
│       └── PurchasesEndpoints.cs
│
├── Shared/                               # Cross-cutting concerns
│   ├── Data/
│   │   └── NastartDbContext.cs
│   ├── Models/
│   │   ├── Result.cs
│   │   └── PagedResult.cs
│   └── Behaviors/
│       ├── ValidationBehavior.cs
│       └── LoggingBehavior.cs
│
├── Program.cs
└── Nastart.Api.csproj
```

### Why This Structure?

| Traditional Layers | Vertical Slices |
|-------------------|-----------------|
| Changes span multiple folders | Changes in one folder |
| Hard to delete features | Delete folder = delete feature |
| Abstractions for abstraction's sake | Concrete implementations |
| Repository, Service, Controller, etc. | Everything in one place |

### Your Task (Day 1):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create feature-based folder structure
mkdir Features\Ingredients
mkdir Features\Recipes
mkdir Features\Purchases
mkdir Features\Alerts
mkdir Shared\Data
mkdir Shared\Models
mkdir Shared\Behaviors

# Remove any placeholder files
Remove-Item Class1.cs -ErrorAction SilentlyContinue
```

---

# Day 2: Models & Database Entities

## 🧒 Explain Like I'm 5

Every app needs to remember things — like a notebook. In our app, we need to remember:
- **Ingredients** (flour, sugar, eggs)
- **Recipes** (chocolate cake, cookies)
- **Purchases** (what we bought and when)

We create **models** — blueprints that describe what each thing looks like.

## 🔧 Engineer Language

**Entity models** represent database tables. In Vertical Slice Architecture, models live inside their feature folders, close to where they're used. We use EF Core conventions and keep models simple.

> 📖 **Microsoft Docs**: *"Entity Framework Core uses a set of conventions to determine how the model maps to a database. You can override or supplement the conventions using configuration."*
>
> — [Creating and Configuring a Model](https://learn.microsoft.com/en-us/ef/core/modeling/)

### The Ingredient Entity:

Create `src/Nastart.Api/Features/Ingredients/Ingredient.cs`:

```csharp
namespace Nastart.Api.Features.Ingredients;

/// <summary>
/// Represents an ingredient tracked in inventory.
/// Entity model for database persistence with EF Core.
/// </summary>
/// <remarks>
/// Following Minimal API conventions — entities are simple POCOs.
/// Business logic is in feature handlers, not in entity classes.
/// See: https://learn.microsoft.com/en-us/ef/core/modeling/
/// </remarks>
public class Ingredient
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Unit { get; set; }
    public decimal CurrentPrice { get; set; }
    public string Currency { get; set; } = "IDR";
    public decimal StockQuantity { get; set; }
    public decimal MinimumStock { get; set; }
    public int? CategoryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation property
    public Category? Category { get; set; }
}

/// <summary>
/// Category for grouping ingredients.
/// </summary>
public class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public ICollection<Ingredient> Ingredients { get; set; } = [];
}
```

### .NET 10 Features Used:

| Feature | Usage |
|---------|-------|
| **File-scoped namespace** | `namespace Nastart.Api.Features.Ingredients;` |
| **Required members** | `required string Name` ensures initialization |
| **Collection expressions** | `Ingredients { get; set; } = [];` |
| **Nullable reference types** | `Category? Category` |

### Install EF Core Packages:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# EF Core with PostgreSQL provider
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design
```

### Your Task (Day 2):

1. Create `Ingredient.cs` in `Features/Ingredients/` folder
2. Install EF Core packages
3. Verify build: `dotnet build`

---

# Day 3: C# Records for Feature Data

## 🧒 Explain Like I'm 5

When you order at a restaurant, you give the waiter a list: "I want pizza with extra cheese." That list is like a **request** — it tells the kitchen exactly what to make.

When they bring your food, they also bring the receipt — that's like a **response**, telling you what you got.

In our app, we use special "lists" called **records** for:
- **Requests** — What the user wants to do
- **Responses** — What we send back
- **Data containers** — Like money amounts or quantities

## 🔧 Engineer Language

**C# Records** are perfect for DTOs (Data Transfer Objects) in feature slices:
- **Immutable by default** — Data set at construction, never changed
- **Built-in equality** — Two records with same values are equal
- **Concise syntax** — Less boilerplate code
- **With-expressions** — Easy to create modified copies

> 📖 **Microsoft Docs**: *"Records provide built-in functionality for encapsulating data. You use record types when you want value equality and minimal mutability."*
>
> — [Records (C# reference)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)

### Request and Response Records:

```csharp
using MediatR;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Ingredients;

// ── Commands (Write Operations) ──

/// <summary>
/// Command to create a new ingredient.
/// Implements IRequest for MediatR handling.
/// </summary>
public sealed record CreateIngredientCommand(
    string Name,
    string Unit,
    decimal InitialPrice,
    decimal MinimumStock,
    int? CategoryId = null
) : IRequest<Result<IngredientResponse>>;

/// <summary>
/// Command to update an ingredient's price.
/// </summary>
public sealed record UpdateIngredientPriceCommand(
    Guid IngredientId,
    decimal NewPrice
) : IRequest<Result<IngredientResponse>>;


// ── Queries (Read Operations) ──

/// <summary>
/// Query to get an ingredient by ID.
/// </summary>
public sealed record GetIngredientQuery(
    Guid Id
) : IRequest<Result<IngredientResponse>>;

/// <summary>
/// Query to get all ingredients with optional filtering.
/// </summary>
public sealed record GetIngredientsQuery(
    string? SearchTerm = null,
    int? CategoryId = null,
    bool? LowStockOnly = null
) : IRequest<Result<IReadOnlyList<IngredientResponse>>>;


// ── Responses (DTOs) ──

/// <summary>
/// Response DTO for ingredient data.
/// </summary>
public sealed record IngredientResponse(
    Guid Id,
    string Name,
    string Unit,
    decimal CurrentPrice,
    string Currency,
    decimal StockQuantity,
    decimal MinimumStock,
    bool IsLowStock,
    int? CategoryId,
    string? CategoryName
);
```

### Data Records (Value-Like Objects):

Create `src/Nastart.Api/Shared/Models/Money.cs`:

```csharp
namespace Nastart.Api.Shared.Models;

/// <summary>
/// Represents a monetary amount with currency.
/// Immutable data container using C# record.
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
    public static Quantity Pieces(decimal value) => new(value, "pcs");

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

### The Result Pattern:

Create `src/Nastart.Api/Shared/Models/Result.cs`:

```csharp
namespace Nastart.Api.Shared.Models;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// Use instead of exceptions for expected failures.
/// </summary>
/// <remarks>
/// This pattern is common in Minimal APIs to avoid throwing exceptions
/// for validation errors or not-found scenarios.
/// See: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses
/// </remarks>
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public Error? Error { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = null;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        Value = default;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);
    
    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>
    /// Pattern match on success or failure.
    /// </summary>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}

/// <summary>
/// Represents an error with a code and message.
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static Error NotFound(string code, string message) => new(code, message);
    public static Error Validation(string code, string message) => new(code, message);
    public static Error Conflict(string code, string message) => new(code, message);
}
```

### Install MediatR Package:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api
dotnet add package MediatR
```

### Your Task (Day 3):

1. Create request/response records in feature folder
2. Create `Money.cs`, `Quantity.cs`, `Result.cs` in `Shared/Models/`
3. Install `MediatR` package
4. Verify build: `dotnet build`

---

# Day 4: MediatR Notifications for Side Effects

## 🧒 Explain Like I'm 5

Imagine you're at a birthday party. When the cake arrives, someone SHOUTS:

> "🎂 THE CAKE IS HERE!"

Everyone hears it! Some people come to take photos, some get plates ready, some start singing. They all REACT to the announcement.

**MediatR Notifications** are those announcements:
- `PriceChangedNotification` — "The flour price changed!"
- `LowStockNotification` — "We're running low on sugar!"
- `MarginBelowThresholdNotification` — "Warning! Cookies profit is too low!"

Different parts of the app HEAR these announcements and react.

## 🔧 Engineer Language

A **MediatR Notification** triggers side effects after a feature handler completes its main work. Multiple handlers can respond to the same notification.

In Vertical Slice Architecture:
1. **Feature handler** does the main work (e.g., update price)
2. **Publishes notification** to signal what happened
3. **Notification handlers** react (e.g., recalculate recipes, send alerts)

> 📖 **Microsoft Docs**: *"MediatR is a library that simplifies CQRS patterns and decouples request processing. It supports notifications where multiple handlers can respond to a single event."*
>
> — [Use MediatR to reduce coupling](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api#use-mediatr-to-reduce-coupling-between-command-handlers)

### Notification Records:

Create `src/Nastart.Api/Features/Ingredients/IngredientNotifications.cs`:

```csharp
using MediatR;

namespace Nastart.Api.Features.Ingredients;

/// <summary>
/// Published when an ingredient's price changes.
/// Triggers recipe cost recalculation.
/// </summary>
public sealed record PriceChangedNotification(
    Guid IngredientId,
    string IngredientName,
    decimal OldPrice,
    decimal NewPrice,
    decimal PercentageChange,
    DateTime OccurredAt
) : INotification
{
    /// <summary>
    /// Returns true if this is a significant price increase (>15%).
    /// </summary>
    public bool IsPriceSpike => PercentageChange > 15;
}

/// <summary>
/// Published when ingredient stock falls below minimum threshold.
/// </summary>
public sealed record LowStockNotification(
    Guid IngredientId,
    string IngredientName,
    decimal CurrentStock,
    decimal MinimumStock,
    string Unit,
    DateTime OccurredAt
) : INotification;

/// <summary>
/// Published when a price spike is detected (>15% increase).
/// Triggers alert creation and notification.
/// </summary>
public sealed record PriceSpikeNotification(
    Guid IngredientId,
    string IngredientName,
    decimal OldPrice,
    decimal NewPrice,
    decimal PercentageIncrease,
    DateTime OccurredAt
) : INotification;
```

### Notification Handlers:

Create `src/Nastart.Api/Features/Ingredients/NotificationHandlers/PriceChangedHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Ingredients.NotificationHandlers;

/// <summary>
/// Handles PriceChangedNotification by recalculating affected recipe costs.
/// </summary>
public class RecalculateRecipeCostsHandler : INotificationHandler<PriceChangedNotification>
{
    private readonly NastartDbContext _db;
    private readonly ILogger<RecalculateRecipeCostsHandler> _logger;

    public RecalculateRecipeCostsHandler(
        NastartDbContext db,
        ILogger<RecalculateRecipeCostsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(PriceChangedNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Recalculating recipes affected by price change: {IngredientName} ({OldPrice} → {NewPrice})",
            notification.IngredientName,
            notification.OldPrice,
            notification.NewPrice);

        // Find recipes that use this ingredient and recalculate
        // Implementation depends on your Recipe feature structure
    }
}

/// <summary>
/// Handles PriceSpikeNotification by creating an alert.
/// </summary>
public class CreatePriceSpikeAlertHandler : INotificationHandler<PriceSpikeNotification>
{
    private readonly NastartDbContext _db;
    private readonly ILogger<CreatePriceSpikeAlertHandler> _logger;

    public CreatePriceSpikeAlertHandler(
        NastartDbContext db,
        ILogger<CreatePriceSpikeAlertHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(PriceSpikeNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Price spike detected: {IngredientName} increased by {Percentage:N1}%",
            notification.IngredientName,
            notification.PercentageIncrease);

        // Create alert in database
        // Implementation depends on your Alert feature structure
    }
}
```

### How Notifications Flow in VSA:

```
┌─────────────────────────┐     ┌────────────────┐     ┌───────────────────────────┐
│  UpdatePriceHandler     │     │  MediatR       │     │  Notification Handlers    │
│  (Feature Slice)        │────▶│  Publish()     │────▶│                           │
│                         │     │                │     │ • RecalculateRecipeCosts  │
│ _mediator.Publish(      │     │                │     │ • CreatePriceSpikeAlert   │
│   new PriceChanged...)  │     │                │     │ • SendTelegramNotification│
└─────────────────────────┘     └────────────────┘     └───────────────────────────┘
```

### Your Task (Day 4):

1. Create `IngredientNotifications.cs` with notification records
2. Create notification handlers in `NotificationHandlers/` subfolder
3. Verify build: `dotnet build`

---

# Day 5: The Feature Slice Pattern

## 🧒 Explain Like I'm 5

When you order at a restaurant:
1. You tell the waiter what you want (**Request**)
2. The kitchen makes your food (**Handler**)
3. The waiter brings your meal (**Response**)

Each dish is a complete "slice" — from order to delivery!

In our app:
- **Request** = What the user wants (e.g., "Create an ingredient called Flour")
- **Handler** = The code that does the work
- **Response** = What we send back (e.g., the created ingredient's details)

## 🔧 Engineer Language

A **Feature Slice** contains everything needed for one user action:

| Component | Purpose | Example |
|-----------|---------|---------|
| **Request** | What the user wants | `CreateIngredientCommand` |
| **Handler** | Processes the request | `CreateIngredientHandler` |
| **Response** | Data returned | `IngredientResponse` |
| **Endpoint** | HTTP route | `POST /api/ingredients` |
| **Validator** | Input validation | `CreateIngredientValidator` |

> 📖 **Microsoft Docs**: *"Route handlers are methods that execute when a request matches a specific route. Minimal APIs allow you to define route handlers as inline lambdas or as method groups."*
>
> — [Route handlers in Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers)

### Anatomy of a Feature Slice:

```csharp
// CreateIngredient.cs — Everything for this feature in ONE file

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Ingredients;

// ── Request ──
public sealed record CreateIngredientCommand(
    string Name,
    string Unit,
    decimal InitialPrice,
    decimal MinimumStock,
    int? CategoryId = null
) : IRequest<Result<IngredientResponse>>;

// ── Validator ──
public sealed class CreateIngredientValidator : AbstractValidator<CreateIngredientCommand>
{
    public CreateIngredientValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name must be 100 characters or less");
        
        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Unit is required")
            .MaximumLength(20).WithMessage("Unit must be 20 characters or less");
        
        RuleFor(x => x.InitialPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Price cannot be negative");
        
        RuleFor(x => x.MinimumStock)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum stock cannot be negative");
    }
}

// ── Handler ──
public sealed class CreateIngredientHandler 
    : IRequestHandler<CreateIngredientCommand, Result<IngredientResponse>>
{
    private readonly NastartDbContext _db;

    public CreateIngredientHandler(NastartDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IngredientResponse>> Handle(
        CreateIngredientCommand request, 
        CancellationToken cancellationToken)
    {
        // Check for duplicate name
        var exists = await _db.Ingredients
            .AnyAsync(i => i.Name == request.Name, cancellationToken);
        
        if (exists)
        {
            return Error.Conflict("DuplicateName", 
                $"Ingredient '{request.Name}' already exists");
        }

        // Create entity
        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Unit = request.Unit,
            CurrentPrice = request.InitialPrice,
            MinimumStock = request.MinimumStock,
            CategoryId = request.CategoryId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Ingredients.Add(ingredient);
        await _db.SaveChangesAsync(cancellationToken);

        // Map to response
        return new IngredientResponse(
            Id: ingredient.Id,
            Name: ingredient.Name,
            Unit: ingredient.Unit,
            CurrentPrice: ingredient.CurrentPrice,
            Currency: ingredient.Currency,
            StockQuantity: ingredient.StockQuantity,
            MinimumStock: ingredient.MinimumStock,
            IsLowStock: ingredient.StockQuantity < ingredient.MinimumStock,
            CategoryId: ingredient.CategoryId,
            CategoryName: null
        );
    }
}

// ── Endpoint (registered in IngredientsEndpoints.cs) ──
// POST /api/ingredients → CreateIngredientCommand → CreateIngredientHandler
```

### Why One File Per Feature?

| Benefit | Explanation |
|---------|-------------|
| **Locality** | All related code is visible at once |
| **Easy deletion** | Remove feature = delete one file |
| **Less navigation** | No jumping between folders |
| **Clear boundaries** | Each file is a complete unit |

### Your Task (Day 5):

1. Understand the feature slice pattern
2. Plan your features as self-contained slices
3. Review MediatR request/handler documentation

---

# Day 6: Building the CreateIngredient Feature

## 🧒 Explain Like I'm 5

Now let's build our first complete feature — **Create Ingredient**!

When someone wants to add a new ingredient:
1. They send the ingredient details (name, unit, price)
2. We check if it's valid (no blank names!)
3. We save it to the database
4. We send back the saved ingredient

## 🔧 Engineer Language

Let's build a complete vertical slice with:
- Minimal API endpoint
- MediatR command and handler
- FluentValidation
- EF Core persistence

### Install FluentValidation:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api
dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions
```

### The Complete CreateIngredient Feature:

Create `src/Nastart.Api/Features/Ingredients/CreateIngredient.cs`:

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Ingredients;

// ══════════════════════════════════════════════════════════════
// CREATE INGREDIENT FEATURE SLICE
// Everything needed to create an ingredient in one file
// ══════════════════════════════════════════════════════════════

// ── Request ──
/// <summary>
/// Command to create a new ingredient.
/// </summary>
public sealed record CreateIngredientCommand(
    string Name,
    string Unit,
    decimal InitialPrice,
    decimal MinimumStock,
    int? CategoryId = null
) : IRequest<Result<IngredientResponse>>;

// ── Response ──
/// <summary>
/// Response DTO for ingredient operations.
/// </summary>
public sealed record IngredientResponse(
    Guid Id,
    string Name,
    string Unit,
    decimal CurrentPrice,
    string Currency,
    decimal StockQuantity,
    decimal MinimumStock,
    bool IsLowStock,
    int? CategoryId,
    string? CategoryName
);

// ── Validator ──
/// <summary>
/// Validates CreateIngredientCommand input.
/// </summary>
public sealed class CreateIngredientValidator : AbstractValidator<CreateIngredientCommand>
{
    public CreateIngredientValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ingredient name is required")
            .MaximumLength(100).WithMessage("Name must be 100 characters or less");
        
        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("Unit is required")
            .MaximumLength(20).WithMessage("Unit must be 20 characters or less");
        
        RuleFor(x => x.InitialPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Price cannot be negative");
        
        RuleFor(x => x.MinimumStock)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum stock cannot be negative");
    }
}

// ── Handler ──
/// <summary>
/// Handles CreateIngredientCommand by creating and persisting the ingredient.
/// </summary>
public sealed class CreateIngredientHandler 
    : IRequestHandler<CreateIngredientCommand, Result<IngredientResponse>>
{
    private readonly NastartDbContext _db;
    private readonly ILogger<CreateIngredientHandler> _logger;

    public CreateIngredientHandler(NastartDbContext db, ILogger<CreateIngredientHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<IngredientResponse>> Handle(
        CreateIngredientCommand request, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating ingredient: {Name}", request.Name);

        // Check for duplicate name
        var exists = await _db.Ingredients
            .AnyAsync(i => i.Name.ToLower() == request.Name.ToLower(), cancellationToken);
        
        if (exists)
        {
            _logger.LogWarning("Duplicate ingredient name: {Name}", request.Name);
            return Error.Conflict("DuplicateName", 
                $"Ingredient '{request.Name}' already exists");
        }

        // Validate category exists if provided
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _db.Categories
                .AnyAsync(c => c.Id == request.CategoryId.Value, cancellationToken);
            
            if (!categoryExists)
            {
                return Error.NotFound("CategoryNotFound", 
                    $"Category with ID {request.CategoryId} not found");
            }
        }

        // Create entity
        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Unit = request.Unit.Trim(),
            CurrentPrice = request.InitialPrice,
            Currency = "IDR",
            StockQuantity = 0,
            MinimumStock = request.MinimumStock,
            CategoryId = request.CategoryId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Ingredients.Add(ingredient);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created ingredient {Id}: {Name}", ingredient.Id, ingredient.Name);

        // Map to response
        return new IngredientResponse(
            Id: ingredient.Id,
            Name: ingredient.Name,
            Unit: ingredient.Unit,
            CurrentPrice: ingredient.CurrentPrice,
            Currency: ingredient.Currency,
            StockQuantity: ingredient.StockQuantity,
            MinimumStock: ingredient.MinimumStock,
            IsLowStock: true, // New ingredient has 0 stock
            CategoryId: ingredient.CategoryId,
            CategoryName: null
        );
    }
}
```

### Register the Endpoint:

Create `src/Nastart.Api/Features/Ingredients/IngredientsEndpoints.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Ingredients;

/// <summary>
/// Registers all ingredient-related endpoints.
/// </summary>
public static class IngredientsEndpoints
{
    public static void MapIngredientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ingredients")
            .WithTags("Ingredients")
            .WithOpenApi();

        group.MapPost("/", CreateIngredient)
            .WithName("CreateIngredient")
            .WithSummary("Create a new ingredient")
            .Produces<IngredientResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    /// <summary>
    /// Create a new ingredient.
    /// </summary>
    private static async Task<Results<Created<IngredientResponse>, Conflict<Error>, ValidationProblem>> 
        CreateIngredient(
            CreateIngredientCommand command,
            IMediator mediator,
            CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.Match<Results<Created<IngredientResponse>, Conflict<Error>, ValidationProblem>>(
            success => TypedResults.Created($"/api/ingredients/{success.Id}", success),
            error => error.Code == "DuplicateName" 
                ? TypedResults.Conflict(error)
                : TypedResults.ValidationProblem(new Dictionary<string, string[]>
                {
                    { "Error", [error.Message] }
                })
        );
    }
}
```

### Register in Program.cs:

```csharp
using Nastart.Api.Features.Ingredients;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);
builder.Services.AddDbContext<NastartDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map endpoints
app.MapIngredientEndpoints();

app.Run();
```

### Your Task (Day 6):

1. Install FluentValidation packages
2. Create `CreateIngredient.cs` with all components
3. Create `IngredientsEndpoints.cs` for route registration
4. Update `Program.cs` to register services and endpoints
5. Verify build: `dotnet build`

---

# Day 7: Testing Your Feature Slices

## 🧒 Explain Like I'm 5

Before you let people ride a new roller coaster, you TEST it first! You make sure:
- It goes where it should
- It doesn't break
- It's safe

We do the same with our code:
- Does creating an ingredient actually save it?
- Does it reject invalid data?
- Does it handle duplicates correctly?

## 🔧 Engineer Language

Unit tests verify that your feature handlers work correctly. We test:
1. **Happy path** — Normal successful operation
2. **Validation** — Invalid input is rejected
3. **Business rules** — Duplicates, not found, etc.
4. **Edge cases** — Boundary conditions

### Set Up Test Project:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend

# Create tests folder if needed
mkdir tests -ErrorAction SilentlyContinue

# Create test project
cd tests
dotnet new xunit -n Nastart.Api.Tests
cd Nastart.Api.Tests

# Add project reference
dotnet add reference ../../src/Nastart.Api/Nastart.Api.csproj

# Install test packages
dotnet add package FluentAssertions
dotnet add package Microsoft.EntityFrameworkCore.InMemory
dotnet add package Moq
```

### CreateIngredient Handler Tests:

Create `tests/Nastart.Api.Tests/Features/Ingredients/CreateIngredientTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Tests.Features.Ingredients;

public class CreateIngredientTests
{
    private readonly NastartDbContext _db;
    private readonly CreateIngredientHandler _handler;

    public CreateIngredientTests()
    {
        // Use in-memory database for testing
        var options = new DbContextOptionsBuilder<NastartDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _db = new NastartDbContext(options);
        var logger = Mock.Of<ILogger<CreateIngredientHandler>>();
        _handler = new CreateIngredientHandler(_db, logger);
    }

    [Fact]
    public async Task Handle_WithValidData_CreatesIngredient()
    {
        // Arrange
        var command = new CreateIngredientCommand(
            Name: "Flour",
            Unit: "kg",
            InitialPrice: 15000,
            MinimumStock: 5
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Flour");
        result.Value.Unit.Should().Be("kg");
        result.Value.CurrentPrice.Should().Be(15000);
        
        // Verify persisted
        var saved = await _db.Ingredients.FirstOrDefaultAsync();
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Flour");
    }

    [Fact]
    public async Task Handle_WithDuplicateName_ReturnsConflictError()
    {
        // Arrange
        _db.Ingredients.Add(new Ingredient
        {
            Id = Guid.NewGuid(),
            Name = "Flour",
            Unit = "kg",
            CurrentPrice = 15000,
            MinimumStock = 5,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var command = new CreateIngredientCommand(
            Name: "Flour",  // Duplicate!
            Unit: "kg",
            InitialPrice: 16000,
            MinimumStock: 3
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("DuplicateName");
    }

    [Fact]
    public async Task Handle_WithInvalidCategory_ReturnsNotFoundError()
    {
        // Arrange
        var command = new CreateIngredientCommand(
            Name: "Sugar",
            Unit: "kg",
            InitialPrice: 12000,
            MinimumStock: 2,
            CategoryId: 999  // Non-existent category
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CategoryNotFound");
    }

    [Fact]
    public async Task Handle_TrimsNameAndUnit()
    {
        // Arrange
        var command = new CreateIngredientCommand(
            Name: "  Flour  ",  // Has spaces
            Unit: " kg ",
            InitialPrice: 15000,
            MinimumStock: 5
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Flour");
        result.Value.Unit.Should().Be("kg");
    }
}
```

### Validator Tests:

Create `tests/Nastart.Api.Tests/Features/Ingredients/CreateIngredientValidatorTests.cs`:

```csharp
using FluentAssertions;
using FluentValidation.TestHelper;
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Tests.Features.Ingredients;

public class CreateIngredientValidatorTests
{
    private readonly CreateIngredientValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_Passes()
    {
        // Arrange
        var command = new CreateIngredientCommand(
            Name: "Flour",
            Unit: "kg",
            InitialPrice: 15000,
            MinimumStock: 5
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithEmptyName_Fails(string? name)
    {
        // Arrange
        var command = new CreateIngredientCommand(
            Name: name!,
            Unit: "kg",
            InitialPrice: 15000,
            MinimumStock: 5
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WithNegativePrice_Fails()
    {
        // Arrange
        var command = new CreateIngredientCommand(
            Name: "Flour",
            Unit: "kg",
            InitialPrice: -100,  // Negative!
            MinimumStock: 5
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.InitialPrice);
    }

    [Fact]
    public void Validate_WithNameTooLong_Fails()
    {
        // Arrange
        var command = new CreateIngredientCommand(
            Name: new string('A', 101),  // 101 characters
            Unit: "kg",
            InitialPrice: 15000,
            MinimumStock: 5
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name must be 100 characters or less");
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
dotnet test --filter "FullyQualifiedName~CreateIngredientTests"
```

### Your Task (Day 7):

1. Set up test project with FluentAssertions and in-memory EF Core
2. Create `CreateIngredientTests.cs` for handler tests
3. Create `CreateIngredientValidatorTests.cs` for validation tests
4. Run `dotnet test` — all tests should pass!

---

# Resources

> ✅ All links verified as of January 2026 using Microsoft Learn

## Vertical Slice Architecture & Minimal APIs

| Resource | Type | Link |
|----------|------|------|
| **Minimal APIs overview** | MS Docs | https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview |
| **Create minimal APIs** | MS Docs | https://learn.microsoft.com/en-us/aspnet/core/tutorials/min-web-api |
| **Route handlers in Minimal APIs** | MS Docs | https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers |
| **Minimal API responses** | MS Docs | https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses |

## MediatR & CQRS Pattern

| Resource | Type | Link |
|----------|------|------|
| **Use MediatR to reduce coupling** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api |
| **CQRS pattern** | MS Docs | https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs |
| **MediatR Library** | GitHub | https://github.com/jbogard/MediatR |

## C# Records & Modern Features

| Resource | Type | Link |
|----------|------|------|
| **Records (C# reference)** | MS Docs | https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record |
| **Introduction to record types** | MS Docs | https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records |
| **Required members** | MS Docs | https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/required |

## Entity Framework Core

| Resource | Type | Link |
|----------|------|------|
| **Creating and Configuring a Model** | MS Docs | https://learn.microsoft.com/en-us/ef/core/modeling/ |
| **DbContext Lifetime** | MS Docs | https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/ |
| **Testing with InMemory** | MS Docs | https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database |

## Validation & Testing

| Resource | Type | Link |
|----------|------|------|
| **FluentValidation Documentation** | Docs | https://docs.fluentvalidation.net/ |
| **Unit testing with xUnit** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-xunit |
| **Best practices for unit testing** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices |
| **FluentAssertions Documentation** | Docs | https://fluentassertions.com/introduction |

## Reference Implementation

| Resource | Type | Link |
|----------|------|------|
| **eShop Reference Application** | GitHub | https://github.com/dotnet/eShop |
| **Vertical Slice Architecture (Jimmy Bogard)** | Blog | https://www.jimmybogard.com/vertical-slice-architecture/ |

---

# Week 2 Checklist

- [ ] **Day 1**: Create Feature Folder Structure
  - [ ] Create `Features/Ingredients/`, `Features/Recipes/`, `Features/Purchases/` folders
  - [ ] Create `Shared/Data/`, `Shared/Models/`, `Shared/Behaviors/` folders
  - [ ] Remove placeholder files

- [ ] **Day 2**: Models & Database Entities
  - [ ] Install EF Core packages
  - [ ] Create `Ingredient.cs` entity in Features folder
  - [ ] Verify build passes

- [ ] **Day 3**: C# Records for Feature Data
  - [ ] Install MediatR package
  - [ ] Create request/response records
  - [ ] Create `Money.cs`, `Quantity.cs`, `Result.cs` in Shared/Models
  - [ ] Verify build passes

- [ ] **Day 4**: MediatR Notifications
  - [ ] Create `IngredientNotifications.cs` with notification records
  - [ ] Create notification handlers
  - [ ] Verify build passes

- [ ] **Day 5**: Feature Slice Pattern
  - [ ] Understand the anatomy of a feature slice
  - [ ] Review MediatR handler documentation
  - [ ] Plan your features as self-contained slices

- [ ] **Day 6**: CreateIngredient Feature
  - [ ] Install FluentValidation packages
  - [ ] Create `CreateIngredient.cs` with Command, Validator, Handler
  - [ ] Create `IngredientsEndpoints.cs` for route registration
  - [ ] Update `Program.cs` with service registration
  - [ ] Verify build passes

- [ ] **Day 7**: Testing Feature Slices
  - [ ] Create test project with required packages
  - [ ] Create `CreateIngredientTests.cs` for handler tests
  - [ ] Create `CreateIngredientValidatorTests.cs` for validation tests
  - [ ] All tests pass with `dotnet test`

---

## File Summary

After completing Week 2, your API project should have:

```
src/Nastart.Api/
├── Features/
│   ├── Ingredients/
│   │   ├── Ingredient.cs                  # Entity model
│   │   ├── CreateIngredient.cs            # Complete feature slice
│   │   ├── IngredientNotifications.cs     # Notifications
│   │   ├── NotificationHandlers/
│   │   │   └── PriceChangedHandler.cs
│   │   └── IngredientsEndpoints.cs        # Route registration
│   ├── Recipes/
│   └── Purchases/
│
├── Shared/
│   ├── Data/
│   │   └── NastartDbContext.cs
│   ├── Models/
│   │   ├── Money.cs
│   │   ├── Quantity.cs
│   │   └── Result.cs
│   └── Behaviors/
│
├── Program.cs
└── Nastart.Api.csproj

tests/Nastart.Api.Tests/
├── Features/
│   └── Ingredients/
│       ├── CreateIngredientTests.cs
│       └── CreateIngredientValidatorTests.cs
└── Nastart.Api.Tests.csproj
```

---

**Next Week**: [Week 3 - Finance Features](./week-03-finance-domain.md)

---

*Remember: In Vertical Slice Architecture, each feature is a complete unit. Everything you need is in one place!* 🎯
