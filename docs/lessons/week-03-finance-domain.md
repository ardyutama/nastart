# Week 3: Purchase Features 💰

> **Goal**: Build the Purchase feature slices — record purchases from receipts, track price changes, and detect price spikes that notify the user.

---

## Table of Contents
1. [Day 1: Purchase & PurchaseItem Models](#day-1-purchase--purchaseitem-models)
2. [Day 2: RecordPurchase Feature Slice](#day-2-recordpurchase-feature-slice)
3. [Day 3: GetPurchaseHistory Query](#day-3-getpurchasehistory-query)
4. [Day 4: Price Change Detection](#day-4-price-change-detection)
5. [Day 5: PriceChangedNotification Flow](#day-5-pricechangednotification-flow)
6. [Day 6: PriceSpikeNotification & Alerts](#day-6-pricespikenotification--alerts)
7. [Day 7: Testing Purchase Features](#day-7-testing-purchase-features)
8. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1: Purchase & PurchaseItem Models

## 🧒 Explain Like I'm 5

When mom goes shopping, she gets a **receipt** — a paper that shows what she bought:
- 2kg flour — Rp 25.000
- 1 dozen eggs — Rp 35.000
- 500g butter — Rp 45.000
- **Total: Rp 105.000**

In our app:
- The **Purchase** is like the whole receipt (where you shopped, when, total cost)
- Each **PurchaseItem** is one line on the receipt (which ingredient, how much, what price)

## 🔧 Engineer Language

A **Purchase** represents a shopping transaction — the header of a receipt. **PurchaseItem** represents each line item. In Vertical Slice Architecture, these models live inside the `Features/Purchases/` folder.

> 📖 **Microsoft Docs**: *"Entity types are typically mapped to tables. EF Core can model one-to-many relationships using navigation properties."*
>
> — [Relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships)

### Purchase Entity Model:

Create `src/Nastart.Api/Features/Purchases/Purchase.cs`:

```csharp
namespace Nastart.Api.Features.Purchases;

/// <summary>
/// Represents a shopping transaction (receipt).
/// Contains multiple PurchaseItems as line items.
/// </summary>
/// <remarks>
/// Entity model following EF Core conventions.
/// Business logic is in feature handlers, not entity classes.
/// See: https://learn.microsoft.com/en-us/ef/core/modeling/
/// </remarks>
public class Purchase
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// The user who made this purchase.
    /// </summary>
    public required Guid UserId { get; set; }
    
    /// <summary>
    /// Shop where purchase was made (optional).
    /// </summary>
    public Guid? ShopId { get; set; }
    
    /// <summary>
    /// Date of purchase.
    /// </summary>
    public required DateOnly PurchaseDate { get; set; }
    
    /// <summary>
    /// URL to the receipt image in storage (optional).
    /// </summary>
    public string? ReceiptImageUrl { get; set; }
    
    /// <summary>
    /// Total amount of the purchase.
    /// </summary>
    public decimal Total { get; set; }
    
    /// <summary>
    /// Processing status: Received, Processing, Parsed, Confirmed, Saved.
    /// </summary>
    public required string Status { get; set; } = "Received";
    
    /// <summary>
    /// When this purchase was recorded.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public Shop? Shop { get; set; }
    public ICollection<PurchaseItem> Items { get; set; } = [];
}

/// <summary>
/// Represents one line item on a purchase receipt.
/// </summary>
public class PurchaseItem
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Parent purchase this item belongs to.
    /// </summary>
    public required Guid PurchaseId { get; set; }
    
    /// <summary>
    /// The ingredient purchased.
    /// </summary>
    public required Guid IngredientId { get; set; }
    
    /// <summary>
    /// Quantity purchased (e.g., 2.5 for 2.5 kg).
    /// </summary>
    public required decimal Quantity { get; set; }
    
    /// <summary>
    /// Unit price at time of purchase.
    /// </summary>
    public required decimal UnitPrice { get; set; }
    
    /// <summary>
    /// Line total (Quantity × UnitPrice).
    /// </summary>
    public decimal LineTotal => Quantity * UnitPrice;
    
    // Navigation properties
    public Purchase? Purchase { get; set; }
    public Ingredient? Ingredient { get; set; }
}

/// <summary>
/// Represents a shop/supplier where purchases are made.
/// </summary>
public class Shop
{
    public Guid Id { get; set; }
    
    public required Guid UserId { get; set; }
    
    public required string Name { get; set; }
    
    public string? Address { get; set; }
    
    public ICollection<Purchase> Purchases { get; set; } = [];
}
```

### Add DbSets to Context:

Update `src/Nastart.Api/Shared/Data/NastartDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Features.Purchases;

namespace Nastart.Api.Shared.Data;

public class NastartDbContext : DbContext
{
    public NastartDbContext(DbContextOptions<NastartDbContext> options) 
        : base(options) { }
    
    // Ingredients
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Category> Categories => Set<Category>();
    
    // Purchases
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<Shop> Shops => Set<Shop>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure Purchase -> PurchaseItem relationship
        modelBuilder.Entity<Purchase>()
            .HasMany(p => p.Items)
            .WithOne(i => i.Purchase)
            .HasForeignKey(i => i.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Configure PurchaseItem -> Ingredient relationship
        modelBuilder.Entity<PurchaseItem>()
            .HasOne(pi => pi.Ingredient)
            .WithMany()
            .HasForeignKey(pi => pi.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Configure decimal precision for money
        modelBuilder.Entity<Purchase>()
            .Property(p => p.Total)
            .HasPrecision(18, 2);
        
        modelBuilder.Entity<PurchaseItem>()
            .Property(pi => pi.UnitPrice)
            .HasPrecision(18, 2);
    }
}
```

### .NET 10 Features Used:

| Feature | Usage |
|---------|-------|
| **Required members** | `required Guid UserId` ensures initialization |
| **Collection expressions** | `Items { get; set; } = [];` |
| **Computed property** | `LineTotal => Quantity * UnitPrice` |
| **File-scoped namespace** | `namespace Nastart.Api.Features.Purchases;` |

### Your Task (Day 1):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create Purchases feature folder
mkdir Features\Purchases

# Create the Purchase model
New-Item Features\Purchases\Purchase.cs

# Verify build
dotnet build
```

---

# Day 2: RecordPurchase Feature Slice

## 🧒 Explain Like I'm 5

When someone finishes shopping and wants to save the receipt:
1. They tell us what they bought (the items list)
2. We check if it makes sense (prices aren't weird)
3. We save everything to our notebook (database)
4. We tell them "Done! Here's your purchase ID"

## 🔧 Engineer Language

The **RecordPurchase** feature slice handles creating a new purchase with its items. This is a **Command** (changes state) that includes validation and persists to the database.

> 📖 **Microsoft Docs**: *"Minimal APIs supports binding to complex types. The framework binds route parameters, query string values, headers, and body content."*
>
> — [Parameter binding in Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding)

### Complete RecordPurchase Feature:

Create `src/Nastart.Api/Features/Purchases/RecordPurchase.cs`:

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Purchases;

// ══════════════════════════════════════════════════════════════
// RECORD PURCHASE FEATURE SLICE
// Everything needed to record a purchase in one file
// ══════════════════════════════════════════════════════════════

// ── Request ──
/// <summary>
/// Command to record a new purchase with items.
/// </summary>
public sealed record RecordPurchaseCommand(
    Guid UserId,
    DateOnly PurchaseDate,
    Guid? ShopId,
    string? ReceiptImageUrl,
    IReadOnlyList<PurchaseItemInput> Items
) : IRequest<Result<PurchaseResponse>>;

/// <summary>
/// Input for a single purchase item.
/// </summary>
public sealed record PurchaseItemInput(
    Guid IngredientId,
    decimal Quantity,
    decimal UnitPrice
);

// ── Response ──
/// <summary>
/// Response DTO for purchase operations.
/// </summary>
public sealed record PurchaseResponse(
    Guid Id,
    DateOnly PurchaseDate,
    string? ShopName,
    decimal Total,
    string Status,
    IReadOnlyList<PurchaseItemResponse> Items,
    DateTime CreatedAt
);

/// <summary>
/// Response DTO for a purchase item.
/// </summary>
public sealed record PurchaseItemResponse(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal LineTotal
);

// ── Validator ──
/// <summary>
/// Validates RecordPurchaseCommand input.
/// </summary>
/// <remarks>
/// FluentValidation provides rich, fluent validation rules.
/// See: https://docs.fluentvalidation.net/
/// </remarks>
public sealed class RecordPurchaseValidator : AbstractValidator<RecordPurchaseCommand>
{
    public RecordPurchaseValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
        
        RuleFor(x => x.PurchaseDate)
            .NotEmpty()
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Purchase date cannot be in the future");
        
        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("At least one item is required");
        
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.IngredientId)
                .NotEmpty()
                .WithMessage("Ingredient ID is required");
            
            item.RuleFor(i => i.Quantity)
                .GreaterThan(0)
                .WithMessage("Quantity must be greater than 0");
            
            item.RuleFor(i => i.UnitPrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Unit price cannot be negative");
        });
    }
}

// ── Handler ──
/// <summary>
/// Handles RecordPurchaseCommand by creating purchase and items.
/// Also triggers price update notifications.
/// </summary>
public sealed class RecordPurchaseHandler 
    : IRequestHandler<RecordPurchaseCommand, Result<PurchaseResponse>>
{
    private readonly NastartDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<RecordPurchaseHandler> _logger;

    public RecordPurchaseHandler(
        NastartDbContext db, 
        IMediator mediator,
        ILogger<RecordPurchaseHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<PurchaseResponse>> Handle(
        RecordPurchaseCommand request, 
        CancellationToken cancellationToken)
    {
        // Validate all ingredients exist
        var ingredientIds = request.Items.Select(i => i.IngredientId).Distinct().ToList();
        var ingredients = await _db.Ingredients
            .Where(i => ingredientIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);
        
        var missingIngredients = ingredientIds.Except(ingredients.Keys).ToList();
        if (missingIngredients.Count > 0)
        {
            return Result<PurchaseResponse>.Failure(
                Error.NotFound("INGREDIENTS_NOT_FOUND", 
                    $"Ingredients not found: {string.Join(", ", missingIngredients)}"));
        }
        
        // Get shop name if provided
        string? shopName = null;
        if (request.ShopId.HasValue)
        {
            var shop = await _db.Shops
                .FirstOrDefaultAsync(s => s.Id == request.ShopId.Value, cancellationToken);
            shopName = shop?.Name;
        }
        
        // Create purchase
        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            PurchaseDate = request.PurchaseDate,
            ShopId = request.ShopId,
            ReceiptImageUrl = request.ReceiptImageUrl,
            Status = "Confirmed",
            CreatedAt = DateTime.UtcNow
        };
        
        // Create purchase items
        var itemResponses = new List<PurchaseItemResponse>();
        var priceNotifications = new List<PriceChangedNotification>();
        
        foreach (var itemInput in request.Items)
        {
            var ingredient = ingredients[itemInput.IngredientId];
            var previousPrice = ingredient.CurrentPrice;
            
            var purchaseItem = new PurchaseItem
            {
                Id = Guid.NewGuid(),
                PurchaseId = purchase.Id,
                IngredientId = itemInput.IngredientId,
                Quantity = itemInput.Quantity,
                UnitPrice = itemInput.UnitPrice
            };
            
            purchase.Items.Add(purchaseItem);
            
            // Check for price change
            if (itemInput.UnitPrice != previousPrice)
            {
                var percentageChange = previousPrice > 0 
                    ? ((itemInput.UnitPrice - previousPrice) / previousPrice) * 100
                    : 0;
                
                // Update ingredient's current price
                ingredient.CurrentPrice = itemInput.UnitPrice;
                ingredient.UpdatedAt = DateTime.UtcNow;
                
                priceNotifications.Add(new PriceChangedNotification(
                    IngredientId: ingredient.Id,
                    IngredientName: ingredient.Name,
                    OldPrice: previousPrice,
                    NewPrice: itemInput.UnitPrice,
                    PercentageChange: percentageChange,
                    OccurredAt: DateTime.UtcNow
                ));
                
                _logger.LogInformation(
                    "Price changed for {Ingredient}: {OldPrice} → {NewPrice} ({Change:+0.0;-0.0}%)",
                    ingredient.Name, previousPrice, itemInput.UnitPrice, percentageChange);
            }
            
            // Update ingredient stock
            ingredient.CurrentStock += itemInput.Quantity;
            
            itemResponses.Add(new PurchaseItemResponse(
                Id: purchaseItem.Id,
                IngredientId: ingredient.Id,
                IngredientName: ingredient.Name,
                Quantity: itemInput.Quantity,
                Unit: ingredient.Unit,
                UnitPrice: itemInput.UnitPrice,
                LineTotal: purchaseItem.LineTotal
            ));
        }
        
        // Calculate total
        purchase.Total = purchase.Items.Sum(i => i.LineTotal);
        
        // Save to database
        _db.Purchases.Add(purchase);
        await _db.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Recorded purchase {PurchaseId} with {ItemCount} items, total: {Total}",
            purchase.Id, purchase.Items.Count, purchase.Total);
        
        // Publish price change notifications (side effects)
        foreach (var notification in priceNotifications)
        {
            await _mediator.Publish(notification, cancellationToken);
        }
        
        return Result<PurchaseResponse>.Success(new PurchaseResponse(
            Id: purchase.Id,
            PurchaseDate: purchase.PurchaseDate,
            ShopName: shopName,
            Total: purchase.Total,
            Status: purchase.Status,
            Items: itemResponses,
            CreatedAt: purchase.CreatedAt
        ));
    }
}
```

### Feature Breakdown:

| Component | Purpose |
|-----------|---------|
| `RecordPurchaseCommand` | Input with all purchase data |
| `PurchaseItemInput` | Nested input for each line item |
| `PurchaseResponse` | Output with saved purchase details |
| `RecordPurchaseValidator` | Validates items, prices, dates |
| `RecordPurchaseHandler` | Creates purchase, updates prices, publishes notifications |

### Your Task (Day 2):

1. Create `RecordPurchase.cs` in `Features/Purchases/`
2. Ensure `PriceChangedNotification` exists from Week 2
3. Verify build: `dotnet build`

---

# Day 3: GetPurchaseHistory Query

## 🧒 Explain Like I'm 5

Sometimes you want to look back at all your old receipts:
- "What did I buy last week?"
- "How much did I spend on flour this month?"
- "Show me all my purchases from the big supermarket"

This is like opening a filing cabinet and finding all your old receipts!

## 🔧 Engineer Language

The **GetPurchaseHistory** feature is a **Query** (reads state). It returns paginated results with filtering options — a common pattern for list endpoints.

> 📖 **Microsoft Docs**: *"Pagination in web apps is important for performance. Returning all entities from a database can overwhelm clients and servers."*
>
> — [Pagination in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/data/ef-mvc/sort-filter-page#add-paging)

### Complete GetPurchaseHistory Feature:

Create `src/Nastart.Api/Features/Purchases/GetPurchaseHistory.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Purchases;

// ══════════════════════════════════════════════════════════════
// GET PURCHASE HISTORY FEATURE SLICE
// Query with pagination and filtering
// ══════════════════════════════════════════════════════════════

// ── Request ──
/// <summary>
/// Query to get purchase history with pagination and filtering.
/// </summary>
public sealed record GetPurchaseHistoryQuery(
    Guid UserId,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Guid? ShopId = null,
    Guid? IngredientId = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<PagedResult<PurchaseSummaryResponse>>>;

// ── Response ──
/// <summary>
/// Summary response for purchase history listing.
/// </summary>
public sealed record PurchaseSummaryResponse(
    Guid Id,
    DateOnly PurchaseDate,
    string? ShopName,
    decimal Total,
    int ItemCount,
    string Status
);

// ── Handler ──
/// <summary>
/// Handles GetPurchaseHistoryQuery with efficient EF Core queries.
/// </summary>
public sealed class GetPurchaseHistoryHandler 
    : IRequestHandler<GetPurchaseHistoryQuery, Result<PagedResult<PurchaseSummaryResponse>>>
{
    private readonly NastartDbContext _db;

    public GetPurchaseHistoryHandler(NastartDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PagedResult<PurchaseSummaryResponse>>> Handle(
        GetPurchaseHistoryQuery request, 
        CancellationToken cancellationToken)
    {
        // Build query with filters
        var query = _db.Purchases
            .Where(p => p.UserId == request.UserId)
            .AsQueryable();
        
        // Apply date filters
        if (request.FromDate.HasValue)
        {
            query = query.Where(p => p.PurchaseDate >= request.FromDate.Value);
        }
        
        if (request.ToDate.HasValue)
        {
            query = query.Where(p => p.PurchaseDate <= request.ToDate.Value);
        }
        
        // Filter by shop
        if (request.ShopId.HasValue)
        {
            query = query.Where(p => p.ShopId == request.ShopId.Value);
        }
        
        // Filter by ingredient (purchases containing this ingredient)
        if (request.IngredientId.HasValue)
        {
            query = query.Where(p => 
                p.Items.Any(i => i.IngredientId == request.IngredientId.Value));
        }
        
        // Get total count for pagination
        var totalCount = await query.CountAsync(cancellationToken);
        
        // Apply pagination and projection
        var purchases = await query
            .OrderByDescending(p => p.PurchaseDate)
            .ThenByDescending(p => p.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(p => new PurchaseSummaryResponse(
                p.Id,
                p.PurchaseDate,
                p.Shop != null ? p.Shop.Name : null,
                p.Total,
                p.Items.Count,
                p.Status
            ))
            .ToListAsync(cancellationToken);
        
        return Result<PagedResult<PurchaseSummaryResponse>>.Success(
            new PagedResult<PurchaseSummaryResponse>(
                Items: purchases,
                TotalCount: totalCount,
                Page: request.Page,
                PageSize: request.PageSize
            ));
    }
}
```

### Add PagedResult to Shared Models:

Create/update `src/Nastart.Api/Shared/Models/PagedResult.cs`:

```csharp
namespace Nastart.Api.Shared.Models;

/// <summary>
/// Represents a paginated result set.
/// </summary>
/// <remarks>
/// Common pattern for list APIs with pagination metadata.
/// See: https://learn.microsoft.com/en-us/aspnet/core/data/ef-mvc/sort-filter-page
/// </remarks>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize
)
{
    /// <summary>
    /// Total number of pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    
    /// <summary>
    /// Whether there's a next page.
    /// </summary>
    public bool HasNextPage => Page < TotalPages;
    
    /// <summary>
    /// Whether there's a previous page.
    /// </summary>
    public bool HasPreviousPage => Page > 1;
}
```

### Get Single Purchase Detail:

Create `src/Nastart.Api/Features/Purchases/GetPurchase.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Purchases;

// ══════════════════════════════════════════════════════════════
// GET PURCHASE DETAIL FEATURE SLICE
// ══════════════════════════════════════════════════════════════

// ── Request ──
public sealed record GetPurchaseQuery(
    Guid PurchaseId,
    Guid UserId
) : IRequest<Result<PurchaseResponse>>;

// ── Handler ──
public sealed class GetPurchaseHandler 
    : IRequestHandler<GetPurchaseQuery, Result<PurchaseResponse>>
{
    private readonly NastartDbContext _db;

    public GetPurchaseHandler(NastartDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PurchaseResponse>> Handle(
        GetPurchaseQuery request, 
        CancellationToken cancellationToken)
    {
        var purchase = await _db.Purchases
            .Include(p => p.Shop)
            .Include(p => p.Items)
                .ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(p => 
                p.Id == request.PurchaseId && 
                p.UserId == request.UserId, 
                cancellationToken);
        
        if (purchase is null)
        {
            return Result<PurchaseResponse>.Failure(
                Error.NotFound("PURCHASE_NOT_FOUND", 
                    $"Purchase {request.PurchaseId} not found"));
        }
        
        return Result<PurchaseResponse>.Success(new PurchaseResponse(
            Id: purchase.Id,
            PurchaseDate: purchase.PurchaseDate,
            ShopName: purchase.Shop?.Name,
            Total: purchase.Total,
            Status: purchase.Status,
            Items: purchase.Items.Select(i => new PurchaseItemResponse(
                Id: i.Id,
                IngredientId: i.IngredientId,
                IngredientName: i.Ingredient?.Name ?? "Unknown",
                Quantity: i.Quantity,
                Unit: i.Ingredient?.Unit ?? "",
                UnitPrice: i.UnitPrice,
                LineTotal: i.LineTotal
            )).ToList(),
            CreatedAt: purchase.CreatedAt
        ));
    }
}
```

### Your Task (Day 3):

1. Create `GetPurchaseHistory.cs` and `GetPurchase.cs`
2. Create/update `PagedResult.cs` in `Shared/Models/`
3. Verify build: `dotnet build`

---

# Day 4: Price Change Detection

## 🧒 Explain Like I'm 5

Imagine you always buy candy for Rp 5.000. One day, the store says "Now it's Rp 6.000!"

You'd want to know:
- "Wait, the price went UP!"
- "It went up by Rp 1.000"
- "That's 20% more expensive!"

Our app does this automatically — every time you record a purchase, it checks if prices changed.

## 🔧 Engineer Language

**Price change detection** happens inside the `RecordPurchaseHandler`. When we record a purchase, we compare the new price with the ingredient's `CurrentPrice`. If they differ, we:
1. Update the ingredient's current price
2. Calculate the percentage change
3. Publish a `PriceChangedNotification`

> 📖 **Microsoft Docs**: *"IMediator.Publish sends a notification to multiple handlers. Unlike Send which targets a single handler, Publish broadcasts to all registered handlers."*
>
> — [MediatR Notifications](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api#use-mediatr-to-reduce-coupling-between-command-handlers)

### Price History Tracking:

Create `src/Nastart.Api/Features/Purchases/PriceHistory.cs`:

```csharp
namespace Nastart.Api.Features.Purchases;

/// <summary>
/// Tracks price changes for ingredients over time.
/// Created automatically when prices change during purchase recording.
/// </summary>
public class PriceHistory
{
    public Guid Id { get; set; }
    
    public required Guid IngredientId { get; set; }
    
    /// <summary>
    /// Date when price was recorded.
    /// </summary>
    public required DateOnly RecordedDate { get; set; }
    
    /// <summary>
    /// The new price.
    /// </summary>
    public required decimal Price { get; set; }
    
    /// <summary>
    /// Previous price before this change.
    /// </summary>
    public decimal? PreviousPrice { get; set; }
    
    /// <summary>
    /// Percentage change from previous price.
    /// </summary>
    public decimal? ChangePercent { get; set; }
    
    /// <summary>
    /// ID of the purchase that triggered this price record.
    /// </summary>
    public Guid? PurchaseId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public Ingredient? Ingredient { get; set; }
}
```

### Price Change Notification (from Week 2):

Ensure `src/Nastart.Api/Features/Ingredients/IngredientNotifications.cs` contains:

```csharp
using MediatR;

namespace Nastart.Api.Features.Ingredients;

/// <summary>
/// Published when an ingredient's price changes.
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
    /// Price spike defined as >15% increase.
    /// </summary>
    public bool IsPriceSpike => PercentageChange > 15;
}
```

### Create Price History Handler:

Create `src/Nastart.Api/Features/Purchases/NotificationHandlers/RecordPriceHistoryHandler.cs`:

```csharp
using MediatR;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Purchases.NotificationHandlers;

/// <summary>
/// Records price changes to PriceHistory table for trend analysis.
/// </summary>
public class RecordPriceHistoryHandler : INotificationHandler<PriceChangedNotification>
{
    private readonly NastartDbContext _db;
    private readonly ILogger<RecordPriceHistoryHandler> _logger;

    public RecordPriceHistoryHandler(
        NastartDbContext db,
        ILogger<RecordPriceHistoryHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(
        PriceChangedNotification notification, 
        CancellationToken cancellationToken)
    {
        var priceHistory = new PriceHistory
        {
            Id = Guid.NewGuid(),
            IngredientId = notification.IngredientId,
            RecordedDate = DateOnly.FromDateTime(notification.OccurredAt),
            Price = notification.NewPrice,
            PreviousPrice = notification.OldPrice,
            ChangePercent = notification.PercentageChange,
            CreatedAt = DateTime.UtcNow
        };
        
        _db.Set<PriceHistory>().Add(priceHistory);
        await _db.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Recorded price history for {Ingredient}: {OldPrice} → {NewPrice}",
            notification.IngredientName,
            notification.OldPrice,
            notification.NewPrice);
    }
}
```

### Update DbContext:

Add to `NastartDbContext.cs`:

```csharp
public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
```

### Your Task (Day 4):

1. Create `PriceHistory.cs` model
2. Create `RecordPriceHistoryHandler.cs` notification handler
3. Update `NastartDbContext.cs` with new DbSet
4. Verify build: `dotnet build`

---

# Day 5: PriceChangedNotification Flow

## 🧒 Explain Like I'm 5

When someone shouts "THE PRICE CHANGED!", different people react differently:
- The accountant writes it down in the book
- The chef re-calculates recipe costs
- The manager decides if it's a "big deal" alert

In our app, one notification triggers MULTIPLE handlers — each doing their own job!

## 🔧 Engineer Language

**MediatR Notifications** follow the **pub/sub pattern**. One publisher, multiple subscribers:

```
┌─────────────────┐     ┌───────────────┐     ┌────────────────────────────┐
│ RecordPurchase  │────▶│ MediatR       │────▶│ RecordPriceHistoryHandler  │
│ Handler         │     │ Publish()     │     │ RecalculateRecipeCosts     │
│                 │     │               │     │ CheckForPriceSpikeHandler  │
└─────────────────┘     └───────────────┘     └────────────────────────────┘
```

> 📖 **Microsoft Docs**: *"The notification handler pattern allows multiple handlers to respond to a single event. This is useful for cross-cutting concerns."*
>
> — [MediatR in ASP.NET Core](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api)

### RecalculateRecipeCosts Handler:

Create `src/Nastart.Api/Features/Recipes/NotificationHandlers/RecalculateRecipeCostsHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Recipes.NotificationHandlers;

/// <summary>
/// When ingredient price changes, recalculate all recipes using that ingredient.
/// </summary>
public class RecalculateRecipeCostsHandler : INotificationHandler<PriceChangedNotification>
{
    private readonly NastartDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<RecalculateRecipeCostsHandler> _logger;

    public RecalculateRecipeCostsHandler(
        NastartDbContext db,
        IMediator mediator,
        ILogger<RecalculateRecipeCostsHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(
        PriceChangedNotification notification, 
        CancellationToken cancellationToken)
    {
        // Find all recipes using this ingredient
        var affectedRecipes = await _db.Set<Recipe>()
            .Include(r => r.Items)
                .ThenInclude(i => i.Ingredient)
            .Where(r => r.Items.Any(i => i.IngredientId == notification.IngredientId))
            .ToListAsync(cancellationToken);
        
        if (affectedRecipes.Count == 0)
        {
            return;
        }
        
        _logger.LogInformation(
            "Recalculating costs for {Count} recipes affected by {Ingredient} price change",
            affectedRecipes.Count,
            notification.IngredientName);
        
        foreach (var recipe in affectedRecipes)
        {
            var oldCost = recipe.TotalCost;
            var oldMargin = recipe.MarginPercent;
            
            // Recalculate total cost from all ingredients
            recipe.TotalCost = recipe.Items.Sum(item => 
                (item.Ingredient?.CurrentPrice ?? 0) * item.Quantity);
            
            // Recalculate margin
            if (recipe.SellPrice > 0)
            {
                recipe.MarginPercent = ((recipe.SellPrice - recipe.TotalCost) / recipe.SellPrice) * 100;
            }
            
            _logger.LogInformation(
                "Recipe '{Recipe}': cost {OldCost} → {NewCost}, margin {OldMargin:F1}% → {NewMargin:F1}%",
                recipe.Name, oldCost, recipe.TotalCost, oldMargin, recipe.MarginPercent);
            
            // Check if margin dropped below threshold (e.g., 20%)
            if (recipe.MarginPercent < 20 && oldMargin >= 20)
            {
                await _mediator.Publish(new MarginBelowThresholdNotification(
                    RecipeId: recipe.Id,
                    RecipeName: recipe.Name,
                    NewMargin: recipe.MarginPercent,
                    Threshold: 20,
                    OccurredAt: DateTime.UtcNow
                ), cancellationToken);
            }
        }
        
        await _db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Published when a recipe's margin falls below the minimum threshold.
/// </summary>
public sealed record MarginBelowThresholdNotification(
    Guid RecipeId,
    string RecipeName,
    decimal NewMargin,
    decimal Threshold,
    DateTime OccurredAt
) : INotification;
```

### CheckForPriceSpike Handler:

Create `src/Nastart.Api/Features/Purchases/NotificationHandlers/CheckForPriceSpikeHandler.cs`:

```csharp
using MediatR;
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Features.Purchases.NotificationHandlers;

/// <summary>
/// Checks if a price change qualifies as a "spike" (>15%) and publishes alert notification.
/// </summary>
public class CheckForPriceSpikeHandler : INotificationHandler<PriceChangedNotification>
{
    private readonly IMediator _mediator;
    private readonly ILogger<CheckForPriceSpikeHandler> _logger;

    public CheckForPriceSpikeHandler(
        IMediator mediator,
        ILogger<CheckForPriceSpikeHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(
        PriceChangedNotification notification, 
        CancellationToken cancellationToken)
    {
        if (!notification.IsPriceSpike)
        {
            return;
        }
        
        _logger.LogWarning(
            "🔴 PRICE SPIKE detected for {Ingredient}: {OldPrice} → {NewPrice} ({Change:+0.0}%)",
            notification.IngredientName,
            notification.OldPrice,
            notification.NewPrice,
            notification.PercentageChange);
        
        // Publish price spike notification for alert creation
        await _mediator.Publish(new PriceSpikeNotification(
            IngredientId: notification.IngredientId,
            IngredientName: notification.IngredientName,
            OldPrice: notification.OldPrice,
            NewPrice: notification.NewPrice,
            PercentageChange: notification.PercentageChange,
            OccurredAt: notification.OccurredAt
        ), cancellationToken);
    }
}

/// <summary>
/// Published when a price spike (>15% increase) is detected.
/// </summary>
public sealed record PriceSpikeNotification(
    Guid IngredientId,
    string IngredientName,
    decimal OldPrice,
    decimal NewPrice,
    decimal PercentageChange,
    DateTime OccurredAt
) : INotification;
```

### Notification Flow Diagram:

```
Purchase Recorded
       │
       ▼
PriceChangedNotification
       │
       ├──▶ RecordPriceHistoryHandler    → Saves to PriceHistory table
       │
       ├──▶ RecalculateRecipeCostsHandler → Updates affected recipes
       │           │
       │           └──▶ MarginBelowThresholdNotification (if margin drops)
       │
       └──▶ CheckForPriceSpikeHandler    → If >15% increase
                   │
                   └──▶ PriceSpikeNotification → Creates alert
```

### Your Task (Day 5):

1. Create `Features/Recipes/NotificationHandlers/` folder
2. Create `RecalculateRecipeCostsHandler.cs`
3. Create `CheckForPriceSpikeHandler.cs`
4. Verify build: `dotnet build`

---

# Day 6: PriceSpikeNotification & Alerts

## 🧒 Explain Like I'm 5

When flour suddenly costs WAY more than before, we need to tell the baker right away:

> "🔴 ALERT: Flour price jumped 20%! Was Rp 25.000, now Rp 30.000!"

The baker can then:
- Check if the new price is a mistake
- Decide to find a cheaper supplier
- Raise the prices of their products

## 🔧 Engineer Language

The **Alert** system stores notifications for users. When a `PriceSpikeNotification` is published, an alert handler creates a persistent alert record.

> 📖 **Microsoft Docs**: *"Events represent facts that have occurred. Event handlers perform side effects in response to these facts."*
>
> — [Domain events pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)

### Alert Entity Model:

Create `src/Nastart.Api/Features/Alerts/Alert.cs`:

```csharp
namespace Nastart.Api.Features.Alerts;

/// <summary>
/// Represents a notification/alert for a user.
/// </summary>
public class Alert
{
    public Guid Id { get; set; }
    
    public required Guid UserId { get; set; }
    
    /// <summary>
    /// Alert type: PriceSpike, LowStock, MarginDanger, Inflation.
    /// </summary>
    public required string Type { get; set; }
    
    /// <summary>
    /// Alert severity: Info, Warning, Danger.
    /// </summary>
    public required string Severity { get; set; }
    
    /// <summary>
    /// Human-readable alert message.
    /// </summary>
    public required string Message { get; set; }
    
    /// <summary>
    /// JSON data with alert details for the UI.
    /// </summary>
    public string? Data { get; set; }
    
    /// <summary>
    /// Whether the user has read/dismissed this alert.
    /// </summary>
    public bool IsRead { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ReadAt { get; set; }
}
```

### CreateAlert Feature:

Create `src/Nastart.Api/Features/Alerts/CreateAlert.cs`:

```csharp
using System.Text.Json;
using MediatR;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Alerts;

// ══════════════════════════════════════════════════════════════
// CREATE ALERT FEATURE SLICE
// ══════════════════════════════════════════════════════════════

// ── Request ──
public sealed record CreateAlertCommand(
    Guid UserId,
    string Type,
    string Severity,
    string Message,
    object? Data = null
) : IRequest<Result<AlertResponse>>;

// ── Response ──
public sealed record AlertResponse(
    Guid Id,
    string Type,
    string Severity,
    string Message,
    bool IsRead,
    DateTime CreatedAt
);

// ── Handler ──
public sealed class CreateAlertHandler 
    : IRequestHandler<CreateAlertCommand, Result<AlertResponse>>
{
    private readonly NastartDbContext _db;
    private readonly ILogger<CreateAlertHandler> _logger;

    public CreateAlertHandler(
        NastartDbContext db,
        ILogger<CreateAlertHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<AlertResponse>> Handle(
        CreateAlertCommand request, 
        CancellationToken cancellationToken)
    {
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Type = request.Type,
            Severity = request.Severity,
            Message = request.Message,
            Data = request.Data != null 
                ? JsonSerializer.Serialize(request.Data) 
                : null,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        
        _db.Set<Alert>().Add(alert);
        await _db.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Created {Severity} alert [{Type}] for user {UserId}: {Message}",
            request.Severity, request.Type, request.UserId, request.Message);
        
        return Result<AlertResponse>.Success(new AlertResponse(
            Id: alert.Id,
            Type: alert.Type,
            Severity: alert.Severity,
            Message: alert.Message,
            IsRead: alert.IsRead,
            CreatedAt: alert.CreatedAt
        ));
    }
}
```

### PriceSpike Alert Handler:

Create `src/Nastart.Api/Features/Alerts/NotificationHandlers/CreatePriceSpikeAlertHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Purchases.NotificationHandlers;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Alerts.NotificationHandlers;

/// <summary>
/// Creates an alert when a price spike is detected.
/// </summary>
public class CreatePriceSpikeAlertHandler : INotificationHandler<PriceSpikeNotification>
{
    private readonly NastartDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<CreatePriceSpikeAlertHandler> _logger;

    public CreatePriceSpikeAlertHandler(
        NastartDbContext db,
        IMediator mediator,
        ILogger<CreatePriceSpikeAlertHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(
        PriceSpikeNotification notification, 
        CancellationToken cancellationToken)
    {
        // Get ingredient to find the user
        var ingredient = await _db.Ingredients
            .FirstOrDefaultAsync(i => i.Id == notification.IngredientId, cancellationToken);
        
        if (ingredient is null)
        {
            _logger.LogWarning(
                "Cannot create alert: Ingredient {IngredientId} not found",
                notification.IngredientId);
            return;
        }
        
        // Create alert for the ingredient owner
        var alertData = new
        {
            notification.IngredientId,
            notification.IngredientName,
            notification.OldPrice,
            notification.NewPrice,
            notification.PercentageChange
        };
        
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            UserId = ingredient.UserId,
            Type = "PriceSpike",
            Severity = notification.PercentageChange > 25 ? "Danger" : "Warning",
            Message = $"🔴 Price spike: {notification.IngredientName} increased " +
                      $"{notification.PercentageChange:F1}% " +
                      $"(Rp {notification.OldPrice:N0} → Rp {notification.NewPrice:N0})",
            Data = System.Text.Json.JsonSerializer.Serialize(alertData),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        
        _db.Set<Alert>().Add(alert);
        await _db.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Created price spike alert for user {UserId}: {Message}",
            ingredient.UserId, alert.Message);
    }
}
```

### GetUnreadAlerts Query:

Create `src/Nastart.Api/Features/Alerts/GetUnreadAlerts.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Alerts;

// ══════════════════════════════════════════════════════════════
// GET UNREAD ALERTS FEATURE SLICE
// ══════════════════════════════════════════════════════════════

public sealed record GetUnreadAlertsQuery(
    Guid UserId
) : IRequest<Result<IReadOnlyList<AlertResponse>>>;

public sealed class GetUnreadAlertsHandler 
    : IRequestHandler<GetUnreadAlertsQuery, Result<IReadOnlyList<AlertResponse>>>
{
    private readonly NastartDbContext _db;

    public GetUnreadAlertsHandler(NastartDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<AlertResponse>>> Handle(
        GetUnreadAlertsQuery request, 
        CancellationToken cancellationToken)
    {
        var alerts = await _db.Set<Alert>()
            .Where(a => a.UserId == request.UserId && !a.IsRead)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AlertResponse(
                a.Id,
                a.Type,
                a.Severity,
                a.Message,
                a.IsRead,
                a.CreatedAt
            ))
            .ToListAsync(cancellationToken);
        
        return Result<IReadOnlyList<AlertResponse>>.Success(alerts);
    }
}
```

### Update DbContext:

Add to `NastartDbContext.cs`:

```csharp
public DbSet<Alert> Alerts => Set<Alert>();
```

### Your Task (Day 6):

1. Create `Features/Alerts/` folder structure
2. Create `Alert.cs`, `CreateAlert.cs`, `GetUnreadAlerts.cs`
3. Create `NotificationHandlers/CreatePriceSpikeAlertHandler.cs`
4. Update `NastartDbContext` with Alert DbSet
5. Verify build: `dotnet build`

---

# Day 7: Testing Purchase Features

## 🧒 Explain Like I'm 5

Before selling a toy, the factory tests it:
- Does the button work? ✓
- Does it break easily? ✗
- Does it do what it's supposed to? ✓

We test our code the same way! Each feature gets its own tests.

## 🔧 Engineer Language

Unit tests verify that each feature slice works correctly in isolation. We test:
- **Handlers** — Does the logic work?
- **Validators** — Are invalid inputs rejected?
- **Notifications** — Do side effects trigger?

> 📖 **Microsoft Docs**: *"Unit tests isolate a unit of work from its dependencies, making tests fast and reliable."*
>
> — [Unit testing in .NET](https://learn.microsoft.com/en-us/dotnet/core/testing/)

### Test Project Structure:

```
tests/
└── Nastart.Api.Tests/
    ├── Features/
    │   ├── Ingredients/
    │   │   └── CreateIngredientTests.cs
    │   ├── Purchases/
    │   │   ├── RecordPurchaseTests.cs
    │   │   ├── RecordPurchaseValidatorTests.cs
    │   │   └── GetPurchaseHistoryTests.cs
    │   └── Alerts/
    │       └── CreatePriceSpikeAlertTests.cs
    └── Nastart.Api.Tests.csproj
```

### RecordPurchase Handler Test:

Create `tests/Nastart.Api.Tests/Features/Purchases/RecordPurchaseTests.cs`:

```csharp
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Features.Purchases;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Tests.Features.Purchases;

public class RecordPurchaseTests
{
    private readonly NastartDbContext _db;
    private readonly Mock<IMediator> _mediator;
    private readonly RecordPurchaseHandler _handler;

    public RecordPurchaseTests()
    {
        // In-memory database for testing
        var options = new DbContextOptionsBuilder<NastartDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _db = new NastartDbContext(options);
        _mediator = new Mock<IMediator>();
        var logger = new Mock<ILogger<RecordPurchaseHandler>>();
        
        _handler = new RecordPurchaseHandler(_db, _mediator.Object, logger.Object);
    }

    [Fact]
    public async Task Handle_ValidPurchase_CreatesPurchaseWithItems()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Flour",
            Unit = "kg",
            CurrentPrice = 25000,
            CurrentStock = 0,
            MinimumStock = 5
        };
        _db.Ingredients.Add(ingredient);
        await _db.SaveChangesAsync();
        
        var command = new RecordPurchaseCommand(
            UserId: userId,
            PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow),
            ShopId: null,
            ReceiptImageUrl: null,
            Items: [
                new PurchaseItemInput(
                    IngredientId: ingredient.Id,
                    Quantity: 5,
                    UnitPrice: 26000  // Price increased!
                )
            ]
        );
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Total.Should().Be(130000); // 5 × 26000
        
        // Verify purchase was saved
        var savedPurchase = await _db.Purchases
            .Include(p => p.Items)
            .FirstOrDefaultAsync();
        savedPurchase.Should().NotBeNull();
        savedPurchase!.Items.Should().HaveCount(1);
        
        // Verify ingredient price was updated
        var updatedIngredient = await _db.Ingredients.FindAsync(ingredient.Id);
        updatedIngredient!.CurrentPrice.Should().Be(26000);
        
        // Verify stock was increased
        updatedIngredient.CurrentStock.Should().Be(5);
    }

    [Fact]
    public async Task Handle_PriceChange_PublishesPriceChangedNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Sugar",
            Unit = "kg",
            CurrentPrice = 15000,
            CurrentStock = 0,
            MinimumStock = 5
        };
        _db.Ingredients.Add(ingredient);
        await _db.SaveChangesAsync();
        
        var command = new RecordPurchaseCommand(
            UserId: userId,
            PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow),
            ShopId: null,
            ReceiptImageUrl: null,
            Items: [
                new PurchaseItemInput(
                    IngredientId: ingredient.Id,
                    Quantity: 2,
                    UnitPrice: 18000  // 20% increase!
                )
            ]
        );
        
        // Act
        await _handler.Handle(command, CancellationToken.None);
        
        // Assert — Verify PriceChangedNotification was published
        _mediator.Verify(m => m.Publish(
            It.Is<PriceChangedNotification>(n => 
                n.IngredientId == ingredient.Id &&
                n.OldPrice == 15000 &&
                n.NewPrice == 18000 &&
                n.PercentageChange == 20),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task Handle_IngredientNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new RecordPurchaseCommand(
            UserId: Guid.NewGuid(),
            PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow),
            ShopId: null,
            ReceiptImageUrl: null,
            Items: [
                new PurchaseItemInput(
                    IngredientId: Guid.NewGuid(),  // Non-existent
                    Quantity: 1,
                    UnitPrice: 10000
                )
            ]
        );
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("INGREDIENTS_NOT_FOUND");
    }
}
```

### Validator Tests:

Create `tests/Nastart.Api.Tests/Features/Purchases/RecordPurchaseValidatorTests.cs`:

```csharp
using FluentAssertions;
using Nastart.Api.Features.Purchases;

namespace Nastart.Api.Tests.Features.Purchases;

public class RecordPurchaseValidatorTests
{
    private readonly RecordPurchaseValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_PassesValidation()
    {
        // Arrange
        var command = new RecordPurchaseCommand(
            UserId: Guid.NewGuid(),
            PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow),
            ShopId: null,
            ReceiptImageUrl: null,
            Items: [
                new PurchaseItemInput(Guid.NewGuid(), 1, 10000)
            ]
        );
        
        // Act
        var result = _validator.Validate(command);
        
        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyItems_FailsValidation()
    {
        // Arrange
        var command = new RecordPurchaseCommand(
            UserId: Guid.NewGuid(),
            PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow),
            ShopId: null,
            ReceiptImageUrl: null,
            Items: []  // Empty!
        );
        
        // Act
        var result = _validator.Validate(command);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Items");
    }

    [Fact]
    public void Validate_FutureDate_FailsValidation()
    {
        // Arrange
        var command = new RecordPurchaseCommand(
            UserId: Guid.NewGuid(),
            PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),  // Future!
            ShopId: null,
            ReceiptImageUrl: null,
            Items: [new PurchaseItemInput(Guid.NewGuid(), 1, 10000)]
        );
        
        // Act
        var result = _validator.Validate(command);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PurchaseDate");
    }

    [Fact]
    public void Validate_NegativePrice_FailsValidation()
    {
        // Arrange
        var command = new RecordPurchaseCommand(
            UserId: Guid.NewGuid(),
            PurchaseDate: DateOnly.FromDateTime(DateTime.UtcNow),
            ShopId: null,
            ReceiptImageUrl: null,
            Items: [
                new PurchaseItemInput(Guid.NewGuid(), 1, -100)  // Negative!
            ]
        );
        
        // Act
        var result = _validator.Validate(command);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("UnitPrice"));
    }
}
```

### Run Tests:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend

# Run all tests
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~RecordPurchaseTests"
```

### Your Task (Day 7):

1. Create test files for `RecordPurchase` and `RecordPurchaseValidator`
2. Add test for `GetPurchaseHistory` query
3. Run tests with `dotnet test`
4. Aim for >80% coverage on handlers

---

# Resources

## Microsoft Official Documentation

| Topic | Link |
|-------|------|
| **EF Core Relationships** | [learn.microsoft.com/ef/core/modeling/relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships) |
| **Parameter Binding** | [learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/parameter-binding](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding) |
| **Pagination in ASP.NET** | [learn.microsoft.com/aspnet/core/data/ef-mvc/sort-filter-page](https://learn.microsoft.com/en-us/aspnet/core/data/ef-mvc/sort-filter-page) |
| **MediatR Patterns** | [learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api) |
| **Domain Events** | [learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation) |
| **Unit Testing in .NET** | [learn.microsoft.com/dotnet/core/testing](https://learn.microsoft.com/en-us/dotnet/core/testing/) |
| **FluentValidation Docs** | [docs.fluentvalidation.net](https://docs.fluentvalidation.net/) |

## NuGet Packages Used This Week

```xml
<PackageReference Include="MediatR" Version="12.*" />
<PackageReference Include="FluentValidation" Version="11.*" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.*" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.*" />
```

## Week 3 Checklist

- [ ] Created `Purchase` and `PurchaseItem` entity models
- [ ] Created `RecordPurchase` feature slice with validation
- [ ] Created `GetPurchaseHistory` query with pagination
- [ ] Implemented price change detection in handler
- [ ] Created `PriceChangedNotification` flow
- [ ] Created `PriceSpikeNotification` and alert creation
- [ ] Created `Alert` entity and features
- [ ] Unit tests for all purchase handlers
- [ ] All builds pass: `dotnet build`
- [ ] All tests pass: `dotnet test`

---

## What's Next?

**Week 4: Recipe Features** — Build recipe creation, ingredient management, and cost calculation features with live margin tracking.

---

*Nastart — Start smart, bake profitable*
