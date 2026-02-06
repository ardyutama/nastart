# Week 4: Recipe Features + Alerts 🍳

> **Goal**: Build the Recipe feature slices — create recipes with ingredients, calculate costs in real-time, track profit margins, and trigger alerts when margins fall below threshold.

---

## Table of Contents
1. [Day 1: Recipe & RecipeItem Models](#day-1-recipe--recipeitem-models)
2. [Day 2: CreateRecipe Feature Slice](#day-2-createrecipe-feature-slice)
3. [Day 3: AddIngredientToRecipe Feature](#day-3-addingredienttorecipe-feature)
4. [Day 4: GetRecipeCost Query with Live Calculation](#day-4-getrecipecost-query-with-live-calculation)
5. [Day 5: Margin Calculation & Threshold Detection](#day-5-margin-calculation--threshold-detection)
6. [Day 6: MarginBelowThresholdNotification](#day-6-marginbelowthresholdnotification)
7. [Day 7: Testing Recipe Features](#day-7-testing-recipe-features)
8. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1: Recipe & RecipeItem Models

## 🧒 Explain Like I'm 5

A **recipe** is like a treasure map 🗺️ that tells you:
- What ingredients you need (flour, sugar, eggs)
- How much of each ingredient (500g flour, 200g sugar, 3 eggs)
- How much you sell it for (Rp 50.000 per cake)

The **recipe items** are the individual lines on your map — each ingredient with its amount.

Then we can calculate: "Does this recipe make me money or lose money?"

## 🔧 Engineer Language

A **Recipe** represents a product formula — what ingredients are needed and in what quantities. **RecipeItem** represents each ingredient entry with its quantity. In Vertical Slice Architecture, these models live inside the `Features/Recipes/` folder.

> 📖 **Microsoft Docs**: *"Entity types are typically mapped to tables. Configure entity types in OnModelCreating or use data annotations."*
>
> — [Creating and Configuring a Model](https://learn.microsoft.com/en-us/ef/core/modeling/)

### Recipe Entity Model:

Create `src/Nastart.Api/Features/Recipes/Recipe.cs`:

```csharp
namespace Nastart.Api.Features.Recipes;

/// <summary>
/// Represents a product recipe with ingredients and pricing.
/// </summary>
/// <remarks>
/// Entity model following EF Core conventions.
/// Cost calculations are done in feature handlers, not entity classes.
/// See: https://learn.microsoft.com/en-us/ef/core/modeling/
/// </remarks>
public class Recipe
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// The user who owns this recipe.
    /// </summary>
    public required Guid UserId { get; set; }
    
    /// <summary>
    /// Recipe name (e.g., "Chocolate Cake", "Nastar Cookies").
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Optional description or notes.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Category (e.g., "Cakes", "Cookies", "Bread").
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// The selling price per unit.
    /// </summary>
    public decimal SellPrice { get; set; }
    
    /// <summary>
    /// Calculated total cost of all ingredients.
    /// Updated when ingredients are added/removed or prices change.
    /// </summary>
    public decimal TotalCost { get; set; }
    
    /// <summary>
    /// Calculated margin percentage: ((SellPrice - TotalCost) / SellPrice) * 100.
    /// </summary>
    public decimal MarginPercent { get; set; }
    
    /// <summary>
    /// Recipe status: Draft, Active, NeedsReview, Inactive, Archived.
    /// </summary>
    public required string Status { get; set; } = "Draft";
    
    /// <summary>
    /// Number of units this recipe produces (e.g., "makes 24 cookies").
    /// </summary>
    public int YieldQuantity { get; set; } = 1;
    
    /// <summary>
    /// Unit for yield (e.g., "pieces", "portions", "loaves").
    /// </summary>
    public string YieldUnit { get; set; } = "unit";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public ICollection<RecipeItem> Items { get; set; } = [];
}

/// <summary>
/// Represents one ingredient entry in a recipe.
/// </summary>
public class RecipeItem
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Parent recipe this item belongs to.
    /// </summary>
    public required Guid RecipeId { get; set; }
    
    /// <summary>
    /// The ingredient used.
    /// </summary>
    public required Guid IngredientId { get; set; }
    
    /// <summary>
    /// Quantity of ingredient needed (e.g., 500 for 500g).
    /// </summary>
    public required decimal Quantity { get; set; }
    
    /// <summary>
    /// Calculated cost for this line item (Quantity × Ingredient.CurrentPrice).
    /// </summary>
    public decimal Cost { get; set; }
    
    // Navigation properties
    public Recipe? Recipe { get; set; }
    public Ingredient? Ingredient { get; set; }
}
```

### Add DbSets to Context:

Update `src/Nastart.Api/Shared/Data/NastartDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Features.Purchases;
using Nastart.Api.Features.Recipes;
using Nastart.Api.Features.Alerts;

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
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    
    // Recipes
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();
    
    // Alerts
    public DbSet<Alert> Alerts => Set<Alert>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Recipe -> RecipeItem relationship
        modelBuilder.Entity<Recipe>()
            .HasMany(r => r.Items)
            .WithOne(i => i.Recipe)
            .HasForeignKey(i => i.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // RecipeItem -> Ingredient relationship
        modelBuilder.Entity<RecipeItem>()
            .HasOne(ri => ri.Ingredient)
            .WithMany()
            .HasForeignKey(ri => ri.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Decimal precision for money
        modelBuilder.Entity<Recipe>()
            .Property(r => r.SellPrice)
            .HasPrecision(18, 2);
        
        modelBuilder.Entity<Recipe>()
            .Property(r => r.TotalCost)
            .HasPrecision(18, 2);
        
        modelBuilder.Entity<Recipe>()
            .Property(r => r.MarginPercent)
            .HasPrecision(5, 2);
        
        modelBuilder.Entity<RecipeItem>()
            .Property(ri => ri.Cost)
            .HasPrecision(18, 2);
    }
}
```

### .NET 10 Features Used:

| Feature | Usage |
|---------|-------|
| **Required members** | `required string Name` ensures initialization |
| **Collection expressions** | `Items { get; set; } = [];` |
| **Nullable reference types** | `string? Description` |
| **File-scoped namespace** | `namespace Nastart.Api.Features.Recipes;` |

### Your Task (Day 1):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create Recipes feature folder
mkdir Features\Recipes

# Create the Recipe model
New-Item Features\Recipes\Recipe.cs

# Verify build
dotnet build
```

---

# Day 2: CreateRecipe Feature Slice

## 🧒 Explain Like I'm 5

When a baker wants to add a new recipe:
1. They give it a name ("Chocolate Chip Cookies")
2. They set the selling price (Rp 25.000 per box)
3. They tell us how many it makes (24 cookies)
4. We create a new recipe and say "Done! Now add your ingredients!"

## 🔧 Engineer Language

The **CreateRecipe** feature slice handles creating a new recipe. This is a **Command** (changes state) that creates the recipe header. Ingredients are added separately via the `AddIngredientToRecipe` feature.

> 📖 **Microsoft Docs**: *"Minimal APIs support model binding, validation, and returning appropriate HTTP responses."*
>
> — [Minimal APIs overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview)

### Complete CreateRecipe Feature:

Create `src/Nastart.Api/Features/Recipes/CreateRecipe.cs`:

```csharp
using FluentValidation;
using MediatR;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Recipes;

// ══════════════════════════════════════════════════════════════
// CREATE RECIPE FEATURE SLICE
// Everything needed to create a recipe in one file
// ══════════════════════════════════════════════════════════════

// ── Request ──
/// <summary>
/// Command to create a new recipe.
/// </summary>
public sealed record CreateRecipeCommand(
    Guid UserId,
    string Name,
    decimal SellPrice,
    string? Description = null,
    string? Category = null,
    int YieldQuantity = 1,
    string YieldUnit = "unit"
) : IRequest<Result<RecipeResponse>>;

// ── Response ──
/// <summary>
/// Response DTO for recipe operations.
/// </summary>
public sealed record RecipeResponse(
    Guid Id,
    string Name,
    string? Description,
    string? Category,
    decimal SellPrice,
    decimal TotalCost,
    decimal MarginPercent,
    string Status,
    int YieldQuantity,
    string YieldUnit,
    decimal CostPerUnit,
    IReadOnlyList<RecipeItemResponse> Items,
    DateTime CreatedAt
);

/// <summary>
/// Response DTO for a recipe item.
/// </summary>
public sealed record RecipeItemResponse(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal Cost
);

// ── Validator ──
/// <summary>
/// Validates CreateRecipeCommand input.
/// </summary>
public sealed class CreateRecipeValidator : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
        
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Recipe name is required and must be under 200 characters");
        
        RuleFor(x => x.SellPrice)
            .GreaterThan(0)
            .WithMessage("Selling price must be greater than 0");
        
        RuleFor(x => x.YieldQuantity)
            .GreaterThan(0)
            .WithMessage("Yield quantity must be at least 1");
        
        RuleFor(x => x.YieldUnit)
            .NotEmpty()
            .MaximumLength(50)
            .WithMessage("Yield unit is required");
    }
}

// ── Handler ──
/// <summary>
/// Handles CreateRecipeCommand by creating a new recipe.
/// </summary>
public sealed class CreateRecipeHandler 
    : IRequestHandler<CreateRecipeCommand, Result<RecipeResponse>>
{
    private readonly NastartDbContext _db;
    private readonly ILogger<CreateRecipeHandler> _logger;

    public CreateRecipeHandler(
        NastartDbContext db,
        ILogger<CreateRecipeHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<RecipeResponse>> Handle(
        CreateRecipeCommand request, 
        CancellationToken cancellationToken)
    {
        // Create recipe
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Name = request.Name,
            Description = request.Description,
            Category = request.Category,
            SellPrice = request.SellPrice,
            TotalCost = 0,  // No ingredients yet
            MarginPercent = 100,  // 100% margin when no costs
            Status = "Draft",
            YieldQuantity = request.YieldQuantity,
            YieldUnit = request.YieldUnit,
            CreatedAt = DateTime.UtcNow
        };
        
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Created recipe '{Name}' (ID: {RecipeId}) with sell price {SellPrice}",
            recipe.Name, recipe.Id, recipe.SellPrice);
        
        return Result<RecipeResponse>.Success(new RecipeResponse(
            Id: recipe.Id,
            Name: recipe.Name,
            Description: recipe.Description,
            Category: recipe.Category,
            SellPrice: recipe.SellPrice,
            TotalCost: recipe.TotalCost,
            MarginPercent: recipe.MarginPercent,
            Status: recipe.Status,
            YieldQuantity: recipe.YieldQuantity,
            YieldUnit: recipe.YieldUnit,
            CostPerUnit: 0,
            Items: [],
            CreatedAt: recipe.CreatedAt
        ));
    }
}
```

### GetRecipe Query:

Create `src/Nastart.Api/Features/Recipes/GetRecipe.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Recipes;

// ══════════════════════════════════════════════════════════════
// GET RECIPE FEATURE SLICE
// ══════════════════════════════════════════════════════════════

public sealed record GetRecipeQuery(
    Guid RecipeId,
    Guid UserId
) : IRequest<Result<RecipeResponse>>;

public sealed class GetRecipeHandler 
    : IRequestHandler<GetRecipeQuery, Result<RecipeResponse>>
{
    private readonly NastartDbContext _db;

    public GetRecipeHandler(NastartDbContext db)
    {
        _db = db;
    }

    public async Task<Result<RecipeResponse>> Handle(
        GetRecipeQuery request, 
        CancellationToken cancellationToken)
    {
        var recipe = await _db.Recipes
            .Include(r => r.Items)
                .ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(r => 
                r.Id == request.RecipeId && 
                r.UserId == request.UserId, 
                cancellationToken);
        
        if (recipe is null)
        {
            return Result<RecipeResponse>.Failure(
                Error.NotFound("RECIPE_NOT_FOUND", 
                    $"Recipe {request.RecipeId} not found"));
        }
        
        var costPerUnit = recipe.YieldQuantity > 0 
            ? recipe.TotalCost / recipe.YieldQuantity 
            : 0;
        
        return Result<RecipeResponse>.Success(new RecipeResponse(
            Id: recipe.Id,
            Name: recipe.Name,
            Description: recipe.Description,
            Category: recipe.Category,
            SellPrice: recipe.SellPrice,
            TotalCost: recipe.TotalCost,
            MarginPercent: recipe.MarginPercent,
            Status: recipe.Status,
            YieldQuantity: recipe.YieldQuantity,
            YieldUnit: recipe.YieldUnit,
            CostPerUnit: costPerUnit,
            Items: recipe.Items.Select(i => new RecipeItemResponse(
                Id: i.Id,
                IngredientId: i.IngredientId,
                IngredientName: i.Ingredient?.Name ?? "Unknown",
                Quantity: i.Quantity,
                Unit: i.Ingredient?.Unit ?? "",
                UnitPrice: i.Ingredient?.CurrentPrice ?? 0,
                Cost: i.Cost
            )).ToList(),
            CreatedAt: recipe.CreatedAt
        ));
    }
}
```

### Your Task (Day 2):

1. Create `CreateRecipe.cs` in `Features/Recipes/`
2. Create `GetRecipe.cs` query feature
3. Verify build: `dotnet build`

---

# Day 3: AddIngredientToRecipe Feature

## 🧒 Explain Like I'm 5

Now that we have a recipe, we need to add ingredients!

"For Chocolate Chip Cookies, I need:
- 500g flour
- 200g sugar
- 3 eggs
- 200g chocolate chips"

Each time we add an ingredient, we automatically calculate how much it costs!

## 🔧 Engineer Language

The **AddIngredientToRecipe** feature adds an ingredient with quantity to an existing recipe. It calculates the item cost and updates the recipe's total cost and margin.

> 📖 **Microsoft Docs**: *"Use Include and ThenInclude to specify related data to include in query results."*
>
> — [Loading Related Data](https://learn.microsoft.com/en-us/ef/core/querying/related-data)

### Complete AddIngredientToRecipe Feature:

Create `src/Nastart.Api/Features/Recipes/AddIngredientToRecipe.cs`:

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Recipes;

// ══════════════════════════════════════════════════════════════
// ADD INGREDIENT TO RECIPE FEATURE SLICE
// ══════════════════════════════════════════════════════════════

// ── Request ──
/// <summary>
/// Command to add an ingredient to a recipe.
/// </summary>
public sealed record AddIngredientToRecipeCommand(
    Guid RecipeId,
    Guid UserId,
    Guid IngredientId,
    decimal Quantity
) : IRequest<Result<RecipeResponse>>;

// ── Validator ──
public sealed class AddIngredientToRecipeValidator 
    : AbstractValidator<AddIngredientToRecipeCommand>
{
    public AddIngredientToRecipeValidator()
    {
        RuleFor(x => x.RecipeId)
            .NotEmpty()
            .WithMessage("Recipe ID is required");
        
        RuleFor(x => x.IngredientId)
            .NotEmpty()
            .WithMessage("Ingredient ID is required");
        
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0");
    }
}

// ── Handler ──
/// <summary>
/// Handles adding an ingredient to a recipe.
/// Calculates cost and updates recipe totals.
/// </summary>
public sealed class AddIngredientToRecipeHandler 
    : IRequestHandler<AddIngredientToRecipeCommand, Result<RecipeResponse>>
{
    private readonly NastartDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<AddIngredientToRecipeHandler> _logger;

    public AddIngredientToRecipeHandler(
        NastartDbContext db,
        IMediator mediator,
        ILogger<AddIngredientToRecipeHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<RecipeResponse>> Handle(
        AddIngredientToRecipeCommand request, 
        CancellationToken cancellationToken)
    {
        // Get recipe with items
        var recipe = await _db.Recipes
            .Include(r => r.Items)
                .ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(r => 
                r.Id == request.RecipeId && 
                r.UserId == request.UserId, 
                cancellationToken);
        
        if (recipe is null)
        {
            return Result<RecipeResponse>.Failure(
                Error.NotFound("RECIPE_NOT_FOUND", 
                    $"Recipe {request.RecipeId} not found"));
        }
        
        // Get ingredient
        var ingredient = await _db.Ingredients
            .FirstOrDefaultAsync(i => i.Id == request.IngredientId, cancellationToken);
        
        if (ingredient is null)
        {
            return Result<RecipeResponse>.Failure(
                Error.NotFound("INGREDIENT_NOT_FOUND", 
                    $"Ingredient {request.IngredientId} not found"));
        }
        
        // Check if ingredient already exists in recipe
        var existingItem = recipe.Items
            .FirstOrDefault(i => i.IngredientId == request.IngredientId);
        
        if (existingItem != null)
        {
            // Update existing item
            existingItem.Quantity += request.Quantity;
            existingItem.Cost = existingItem.Quantity * ingredient.CurrentPrice;
            
            _logger.LogInformation(
                "Updated {Ingredient} in recipe '{Recipe}': quantity now {Quantity}",
                ingredient.Name, recipe.Name, existingItem.Quantity);
        }
        else
        {
            // Add new item
            var recipeItem = new RecipeItem
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                IngredientId = ingredient.Id,
                Quantity = request.Quantity,
                Cost = request.Quantity * ingredient.CurrentPrice
            };
            
            recipe.Items.Add(recipeItem);
            
            _logger.LogInformation(
                "Added {Ingredient} to recipe '{Recipe}': {Quantity} {Unit}",
                ingredient.Name, recipe.Name, request.Quantity, ingredient.Unit);
        }
        
        // Recalculate recipe totals
        var oldMargin = recipe.MarginPercent;
        RecalculateRecipeTotals(recipe);
        
        recipe.UpdatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync(cancellationToken);
        
        // Check if margin dropped below threshold
        if (oldMargin >= 20 && recipe.MarginPercent < 20)
        {
            await _mediator.Publish(new MarginBelowThresholdNotification(
                RecipeId: recipe.Id,
                RecipeName: recipe.Name,
                NewMargin: recipe.MarginPercent,
                Threshold: 20,
                OccurredAt: DateTime.UtcNow
            ), cancellationToken);
        }
        
        // Build response
        var costPerUnit = recipe.YieldQuantity > 0 
            ? recipe.TotalCost / recipe.YieldQuantity 
            : 0;
        
        return Result<RecipeResponse>.Success(new RecipeResponse(
            Id: recipe.Id,
            Name: recipe.Name,
            Description: recipe.Description,
            Category: recipe.Category,
            SellPrice: recipe.SellPrice,
            TotalCost: recipe.TotalCost,
            MarginPercent: recipe.MarginPercent,
            Status: recipe.Status,
            YieldQuantity: recipe.YieldQuantity,
            YieldUnit: recipe.YieldUnit,
            CostPerUnit: costPerUnit,
            Items: recipe.Items.Select(i => new RecipeItemResponse(
                Id: i.Id,
                IngredientId: i.IngredientId,
                IngredientName: i.Ingredient?.Name ?? "Unknown",
                Quantity: i.Quantity,
                Unit: i.Ingredient?.Unit ?? "",
                UnitPrice: i.Ingredient?.CurrentPrice ?? 0,
                Cost: i.Cost
            )).ToList(),
            CreatedAt: recipe.CreatedAt
        ));
    }
    
    /// <summary>
    /// Recalculates TotalCost and MarginPercent for a recipe.
    /// </summary>
    private static void RecalculateRecipeTotals(Recipe recipe)
    {
        recipe.TotalCost = recipe.Items.Sum(i => i.Cost);
        
        if (recipe.SellPrice > 0)
        {
            recipe.MarginPercent = ((recipe.SellPrice - recipe.TotalCost) / recipe.SellPrice) * 100;
        }
        else
        {
            recipe.MarginPercent = 0;
        }
    }
}

/// <summary>
/// Published when a recipe's margin falls below threshold.
/// </summary>
public sealed record MarginBelowThresholdNotification(
    Guid RecipeId,
    string RecipeName,
    decimal NewMargin,
    decimal Threshold,
    DateTime OccurredAt
) : INotification;
```

### RemoveIngredientFromRecipe Feature:

Create `src/Nastart.Api/Features/Recipes/RemoveIngredientFromRecipe.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Recipes;

// ══════════════════════════════════════════════════════════════
// REMOVE INGREDIENT FROM RECIPE FEATURE SLICE
// ══════════════════════════════════════════════════════════════

public sealed record RemoveIngredientFromRecipeCommand(
    Guid RecipeId,
    Guid UserId,
    Guid IngredientId
) : IRequest<Result<RecipeResponse>>;

public sealed class RemoveIngredientFromRecipeHandler 
    : IRequestHandler<RemoveIngredientFromRecipeCommand, Result<RecipeResponse>>
{
    private readonly NastartDbContext _db;
    private readonly ILogger<RemoveIngredientFromRecipeHandler> _logger;

    public RemoveIngredientFromRecipeHandler(
        NastartDbContext db,
        ILogger<RemoveIngredientFromRecipeHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Result<RecipeResponse>> Handle(
        RemoveIngredientFromRecipeCommand request, 
        CancellationToken cancellationToken)
    {
        var recipe = await _db.Recipes
            .Include(r => r.Items)
                .ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(r => 
                r.Id == request.RecipeId && 
                r.UserId == request.UserId, 
                cancellationToken);
        
        if (recipe is null)
        {
            return Result<RecipeResponse>.Failure(
                Error.NotFound("RECIPE_NOT_FOUND", 
                    $"Recipe {request.RecipeId} not found"));
        }
        
        var item = recipe.Items
            .FirstOrDefault(i => i.IngredientId == request.IngredientId);
        
        if (item is null)
        {
            return Result<RecipeResponse>.Failure(
                Error.NotFound("ITEM_NOT_FOUND", 
                    $"Ingredient {request.IngredientId} not found in recipe"));
        }
        
        recipe.Items.Remove(item);
        _db.RecipeItems.Remove(item);
        
        // Recalculate totals
        recipe.TotalCost = recipe.Items.Sum(i => i.Cost);
        if (recipe.SellPrice > 0)
        {
            recipe.MarginPercent = ((recipe.SellPrice - recipe.TotalCost) / recipe.SellPrice) * 100;
        }
        
        recipe.UpdatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Removed ingredient from recipe '{Recipe}'",
            recipe.Name);
        
        var costPerUnit = recipe.YieldQuantity > 0 
            ? recipe.TotalCost / recipe.YieldQuantity 
            : 0;
        
        return Result<RecipeResponse>.Success(new RecipeResponse(
            Id: recipe.Id,
            Name: recipe.Name,
            Description: recipe.Description,
            Category: recipe.Category,
            SellPrice: recipe.SellPrice,
            TotalCost: recipe.TotalCost,
            MarginPercent: recipe.MarginPercent,
            Status: recipe.Status,
            YieldQuantity: recipe.YieldQuantity,
            YieldUnit: recipe.YieldUnit,
            CostPerUnit: costPerUnit,
            Items: recipe.Items.Select(i => new RecipeItemResponse(
                Id: i.Id,
                IngredientId: i.IngredientId,
                IngredientName: i.Ingredient?.Name ?? "Unknown",
                Quantity: i.Quantity,
                Unit: i.Ingredient?.Unit ?? "",
                UnitPrice: i.Ingredient?.CurrentPrice ?? 0,
                Cost: i.Cost
            )).ToList(),
            CreatedAt: recipe.CreatedAt
        ));
    }
}
```

### Your Task (Day 3):

1. Create `AddIngredientToRecipe.cs` feature
2. Create `RemoveIngredientFromRecipe.cs` feature
3. Verify build: `dotnet build`

---

# Day 4: GetRecipeCost Query with Live Calculation

## 🧒 Explain Like I'm 5

Remember when flour prices change? We learned about that in Week 3!

Now imagine you have a recipe that uses flour. When flour gets more expensive, YOUR COOKIES also get more expensive to make!

This feature calculates the **live cost** — using today's ingredient prices, not yesterday's.

## 🔧 Engineer Language

The **GetRecipeCost** query calculates the recipe cost using **current ingredient prices**. This is different from stored `TotalCost` which might be outdated if prices changed.

> 📖 **Microsoft Docs**: *"Projection queries retrieve only specific columns, which can improve performance by reducing data transfer."*
>
> — [Querying Data](https://learn.microsoft.com/en-us/ef/core/querying/)

### Complete GetRecipeCost Feature:

Create `src/Nastart.Api/Features/Recipes/GetRecipeCost.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Recipes;

// ══════════════════════════════════════════════════════════════
// GET RECIPE COST FEATURE SLICE
// Calculates live cost using current ingredient prices
// ══════════════════════════════════════════════════════════════

// ── Request ──
public sealed record GetRecipeCostQuery(
    Guid RecipeId,
    Guid UserId
) : IRequest<Result<RecipeCostResponse>>;

// ── Response ──
/// <summary>
/// Detailed cost breakdown for a recipe.
/// </summary>
public sealed record RecipeCostResponse(
    Guid RecipeId,
    string RecipeName,
    decimal SellPrice,
    decimal LiveTotalCost,
    decimal StoredTotalCost,
    decimal CostDifference,
    decimal MarginPercent,
    decimal MarginAmount,
    decimal CostPerUnit,
    decimal ProfitPerUnit,
    int YieldQuantity,
    string YieldUnit,
    string MarginStatus,
    IReadOnlyList<RecipeCostItemResponse> Items
);

/// <summary>
/// Cost breakdown for a single recipe ingredient.
/// </summary>
public sealed record RecipeCostItemResponse(
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    string Unit,
    decimal CurrentUnitPrice,
    decimal LineCost,
    decimal CostPercentage
);

// ── Handler ──
public sealed class GetRecipeCostHandler 
    : IRequestHandler<GetRecipeCostQuery, Result<RecipeCostResponse>>
{
    private readonly NastartDbContext _db;

    public GetRecipeCostHandler(NastartDbContext db)
    {
        _db = db;
    }

    public async Task<Result<RecipeCostResponse>> Handle(
        GetRecipeCostQuery request, 
        CancellationToken cancellationToken)
    {
        // Get recipe with items and current ingredient prices
        var recipe = await _db.Recipes
            .Include(r => r.Items)
                .ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(r => 
                r.Id == request.RecipeId && 
                r.UserId == request.UserId, 
                cancellationToken);
        
        if (recipe is null)
        {
            return Result<RecipeCostResponse>.Failure(
                Error.NotFound("RECIPE_NOT_FOUND", 
                    $"Recipe {request.RecipeId} not found"));
        }
        
        // Calculate live cost using current ingredient prices
        var itemCosts = recipe.Items.Select(item =>
        {
            var currentPrice = item.Ingredient?.CurrentPrice ?? 0;
            var lineCost = item.Quantity * currentPrice;
            
            return new
            {
                Item = item,
                CurrentPrice = currentPrice,
                LineCost = lineCost
            };
        }).ToList();
        
        var liveTotalCost = itemCosts.Sum(x => x.LineCost);
        var storedTotalCost = recipe.TotalCost;
        var costDifference = liveTotalCost - storedTotalCost;
        
        // Calculate margin
        decimal marginPercent = 0;
        decimal marginAmount = 0;
        
        if (recipe.SellPrice > 0)
        {
            marginAmount = recipe.SellPrice - liveTotalCost;
            marginPercent = (marginAmount / recipe.SellPrice) * 100;
        }
        
        // Calculate per-unit metrics
        var costPerUnit = recipe.YieldQuantity > 0 
            ? liveTotalCost / recipe.YieldQuantity 
            : liveTotalCost;
        
        var pricePerUnit = recipe.YieldQuantity > 0 
            ? recipe.SellPrice / recipe.YieldQuantity 
            : recipe.SellPrice;
        
        var profitPerUnit = pricePerUnit - costPerUnit;
        
        // Determine margin status
        var marginStatus = marginPercent switch
        {
            < 0 => "Loss",
            < 10 => "Critical",
            < 20 => "Warning",
            _ => "Healthy"
        };
        
        // Build item responses with cost percentage
        var itemResponses = itemCosts.Select(x => new RecipeCostItemResponse(
            IngredientId: x.Item.IngredientId,
            IngredientName: x.Item.Ingredient?.Name ?? "Unknown",
            Quantity: x.Item.Quantity,
            Unit: x.Item.Ingredient?.Unit ?? "",
            CurrentUnitPrice: x.CurrentPrice,
            LineCost: x.LineCost,
            CostPercentage: liveTotalCost > 0 
                ? (x.LineCost / liveTotalCost) * 100 
                : 0
        )).OrderByDescending(x => x.LineCost).ToList();
        
        return Result<RecipeCostResponse>.Success(new RecipeCostResponse(
            RecipeId: recipe.Id,
            RecipeName: recipe.Name,
            SellPrice: recipe.SellPrice,
            LiveTotalCost: liveTotalCost,
            StoredTotalCost: storedTotalCost,
            CostDifference: costDifference,
            MarginPercent: Math.Round(marginPercent, 2),
            MarginAmount: Math.Round(marginAmount, 2),
            CostPerUnit: Math.Round(costPerUnit, 2),
            ProfitPerUnit: Math.Round(profitPerUnit, 2),
            YieldQuantity: recipe.YieldQuantity,
            YieldUnit: recipe.YieldUnit,
            MarginStatus: marginStatus,
            Items: itemResponses
        ));
    }
}
```

### GetRecipes List Query:

Create `src/Nastart.Api/Features/Recipes/GetRecipes.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Recipes;

// ══════════════════════════════════════════════════════════════
// GET RECIPES LIST FEATURE SLICE
// ══════════════════════════════════════════════════════════════

public sealed record GetRecipesQuery(
    Guid UserId,
    string? Category = null,
    string? Status = null,
    bool? LowMarginOnly = null,
    string? SearchTerm = null
) : IRequest<Result<IReadOnlyList<RecipeSummaryResponse>>>;

public sealed record RecipeSummaryResponse(
    Guid Id,
    string Name,
    string? Category,
    decimal SellPrice,
    decimal TotalCost,
    decimal MarginPercent,
    string Status,
    string MarginStatus,
    int ItemCount
);

public sealed class GetRecipesHandler 
    : IRequestHandler<GetRecipesQuery, Result<IReadOnlyList<RecipeSummaryResponse>>>
{
    private readonly NastartDbContext _db;

    public GetRecipesHandler(NastartDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<RecipeSummaryResponse>>> Handle(
        GetRecipesQuery request, 
        CancellationToken cancellationToken)
    {
        var query = _db.Recipes
            .Include(r => r.Items)
            .Where(r => r.UserId == request.UserId)
            .AsQueryable();
        
        // Apply filters
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(r => r.Category == request.Category);
        }
        
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(r => r.Status == request.Status);
        }
        
        if (request.LowMarginOnly == true)
        {
            query = query.Where(r => r.MarginPercent < 20);
        }
        
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            query = query.Where(r => 
                r.Name.Contains(request.SearchTerm) ||
                (r.Description != null && r.Description.Contains(request.SearchTerm)));
        }
        
        var recipes = await query
            .OrderBy(r => r.Name)
            .Select(r => new RecipeSummaryResponse(
                r.Id,
                r.Name,
                r.Category,
                r.SellPrice,
                r.TotalCost,
                r.MarginPercent,
                r.Status,
                r.MarginPercent < 0 ? "Loss" :
                r.MarginPercent < 10 ? "Critical" :
                r.MarginPercent < 20 ? "Warning" : "Healthy",
                r.Items.Count
            ))
            .ToListAsync(cancellationToken);
        
        return Result<IReadOnlyList<RecipeSummaryResponse>>.Success(recipes);
    }
}
```

### Your Task (Day 4):

1. Create `GetRecipeCost.cs` with live calculation
2. Create `GetRecipes.cs` list query
3. Verify build: `dotnet build`

---

# Day 5: Margin Calculation & Threshold Detection

## 🧒 Explain Like I'm 5

If you sell cookies for Rp 25.000 and they cost Rp 20.000 to make, you only keep Rp 5.000 — that's only 20%!

If ingredients get expensive and now they cost Rp 23.000, you only keep Rp 2.000 — that's just 8%! 😱

We need to WARN the baker when their profit gets too small!

## 🔧 Engineer Language

**Margin threshold detection** identifies when a recipe's profitability falls below acceptable levels. We define thresholds and publish notifications when margins cross them.

> 📖 **Microsoft Docs**: *"Notifications allow multiple handlers to respond to a single event. This is the pub/sub pattern in MediatR."*
>
> — [MediatR Notifications](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api)

### Recipe Notifications:

Create `src/Nastart.Api/Features/Recipes/RecipeNotifications.cs`:

```csharp
using MediatR;

namespace Nastart.Api.Features.Recipes;

/// <summary>
/// Published when a recipe's margin falls below minimum threshold.
/// </summary>
public sealed record MarginBelowThresholdNotification(
    Guid RecipeId,
    string RecipeName,
    decimal NewMargin,
    decimal Threshold,
    DateTime OccurredAt
) : INotification
{
    public bool IsCritical => NewMargin < 10;
    public bool IsLoss => NewMargin < 0;
}

/// <summary>
/// Published when a recipe is created.
/// </summary>
public sealed record RecipeCreatedNotification(
    Guid RecipeId,
    string RecipeName,
    Guid UserId,
    DateTime OccurredAt
) : INotification;

/// <summary>
/// Published when a recipe's cost changes due to ingredient price updates.
/// </summary>
public sealed record RecipeCostChangedNotification(
    Guid RecipeId,
    string RecipeName,
    decimal OldCost,
    decimal NewCost,
    decimal OldMargin,
    decimal NewMargin,
    DateTime OccurredAt
) : INotification
{
    public decimal CostChange => NewCost - OldCost;
    public decimal MarginChange => NewMargin - OldMargin;
}
```

### UpdateRecipeSellPrice Feature:

When sell price changes, we need to recalculate margin:

Create `src/Nastart.Api/Features/Recipes/UpdateRecipeSellPrice.cs`:

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Recipes;

// ══════════════════════════════════════════════════════════════
// UPDATE RECIPE SELL PRICE FEATURE SLICE
// ══════════════════════════════════════════════════════════════

public sealed record UpdateRecipeSellPriceCommand(
    Guid RecipeId,
    Guid UserId,
    decimal NewSellPrice
) : IRequest<Result<RecipeResponse>>;

public sealed class UpdateRecipeSellPriceValidator 
    : AbstractValidator<UpdateRecipeSellPriceCommand>
{
    public UpdateRecipeSellPriceValidator()
    {
        RuleFor(x => x.NewSellPrice)
            .GreaterThan(0)
            .WithMessage("Sell price must be greater than 0");
    }
}

public sealed class UpdateRecipeSellPriceHandler 
    : IRequestHandler<UpdateRecipeSellPriceCommand, Result<RecipeResponse>>
{
    private readonly NastartDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<UpdateRecipeSellPriceHandler> _logger;

    public UpdateRecipeSellPriceHandler(
        NastartDbContext db,
        IMediator mediator,
        ILogger<UpdateRecipeSellPriceHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<RecipeResponse>> Handle(
        UpdateRecipeSellPriceCommand request, 
        CancellationToken cancellationToken)
    {
        var recipe = await _db.Recipes
            .Include(r => r.Items)
                .ThenInclude(i => i.Ingredient)
            .FirstOrDefaultAsync(r => 
                r.Id == request.RecipeId && 
                r.UserId == request.UserId, 
                cancellationToken);
        
        if (recipe is null)
        {
            return Result<RecipeResponse>.Failure(
                Error.NotFound("RECIPE_NOT_FOUND", 
                    $"Recipe {request.RecipeId} not found"));
        }
        
        var oldPrice = recipe.SellPrice;
        var oldMargin = recipe.MarginPercent;
        
        recipe.SellPrice = request.NewSellPrice;
        
        // Recalculate margin
        if (recipe.SellPrice > 0)
        {
            recipe.MarginPercent = 
                ((recipe.SellPrice - recipe.TotalCost) / recipe.SellPrice) * 100;
        }
        
        recipe.UpdatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Updated sell price for '{Recipe}': {OldPrice} → {NewPrice}, margin: {OldMargin:F1}% → {NewMargin:F1}%",
            recipe.Name, oldPrice, request.NewSellPrice, oldMargin, recipe.MarginPercent);
        
        // Check if margin fell below threshold
        if (oldMargin >= 20 && recipe.MarginPercent < 20)
        {
            await _mediator.Publish(new MarginBelowThresholdNotification(
                RecipeId: recipe.Id,
                RecipeName: recipe.Name,
                NewMargin: recipe.MarginPercent,
                Threshold: 20,
                OccurredAt: DateTime.UtcNow
            ), cancellationToken);
        }
        
        var costPerUnit = recipe.YieldQuantity > 0 
            ? recipe.TotalCost / recipe.YieldQuantity 
            : 0;
        
        return Result<RecipeResponse>.Success(new RecipeResponse(
            Id: recipe.Id,
            Name: recipe.Name,
            Description: recipe.Description,
            Category: recipe.Category,
            SellPrice: recipe.SellPrice,
            TotalCost: recipe.TotalCost,
            MarginPercent: recipe.MarginPercent,
            Status: recipe.Status,
            YieldQuantity: recipe.YieldQuantity,
            YieldUnit: recipe.YieldUnit,
            CostPerUnit: costPerUnit,
            Items: recipe.Items.Select(i => new RecipeItemResponse(
                Id: i.Id,
                IngredientId: i.IngredientId,
                IngredientName: i.Ingredient?.Name ?? "Unknown",
                Quantity: i.Quantity,
                Unit: i.Ingredient?.Unit ?? "",
                UnitPrice: i.Ingredient?.CurrentPrice ?? 0,
                Cost: i.Cost
            )).ToList(),
            CreatedAt: recipe.CreatedAt
        ));
    }
}
```

### Your Task (Day 5):

1. Create `RecipeNotifications.cs` with notification records
2. Create `UpdateRecipeSellPrice.cs` feature
3. Verify build: `dotnet build`

---

# Day 6: MarginBelowThresholdNotification

## 🧒 Explain Like I'm 5

When the baker's profit gets too small, we need to:
1. Write it down in our alert book 📓
2. Send the baker a message on Telegram 📱
3. Show a warning on the dashboard 🖥️

This happens AUTOMATICALLY when margins drop!

## 🔧 Engineer Language

The **MarginBelowThresholdNotification** triggers alert creation. Multiple handlers respond: one creates the database alert, another could send Telegram notifications (Week 10).

> 📖 **Microsoft Docs**: *"The notification handler pattern allows cross-cutting concerns to react to domain events without tight coupling."*
>
> — [Domain events](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)

### CreateMarginAlert Handler:

Create `src/Nastart.Api/Features/Recipes/NotificationHandlers/CreateMarginAlertHandler.cs`:

```csharp
using MediatR;
using Nastart.Api.Features.Alerts;
using Nastart.Api.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace Nastart.Api.Features.Recipes.NotificationHandlers;

/// <summary>
/// Creates an alert when a recipe's margin falls below threshold.
/// </summary>
public class CreateMarginAlertHandler : INotificationHandler<MarginBelowThresholdNotification>
{
    private readonly NastartDbContext _db;
    private readonly ILogger<CreateMarginAlertHandler> _logger;

    public CreateMarginAlertHandler(
        NastartDbContext db,
        ILogger<CreateMarginAlertHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(
        MarginBelowThresholdNotification notification, 
        CancellationToken cancellationToken)
    {
        // Get recipe to find user
        var recipe = await _db.Recipes
            .FirstOrDefaultAsync(r => r.Id == notification.RecipeId, cancellationToken);
        
        if (recipe is null)
        {
            _logger.LogWarning(
                "Cannot create alert: Recipe {RecipeId} not found",
                notification.RecipeId);
            return;
        }
        
        // Determine severity
        var severity = notification.NewMargin switch
        {
            < 0 => "Danger",   // Loss
            < 10 => "Danger",  // Critical
            < 20 => "Warning", // Below threshold
            _ => "Warning"
        };
        
        var alertType = notification.IsLoss ? "MarginLoss" : "MarginDanger";
        
        var message = notification.IsLoss
            ? $"🔴 LOSS: Recipe '{notification.RecipeName}' is losing money! Margin: {notification.NewMargin:F1}%"
            : $"🟠 Low margin: Recipe '{notification.RecipeName}' margin dropped to {notification.NewMargin:F1}%";
        
        var alertData = new
        {
            notification.RecipeId,
            notification.RecipeName,
            notification.NewMargin,
            notification.Threshold
        };
        
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            UserId = recipe.UserId,
            Type = alertType,
            Severity = severity,
            Message = message,
            Data = System.Text.Json.JsonSerializer.Serialize(alertData),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        
        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Created {Severity} margin alert for recipe '{Recipe}': {Message}",
            severity, notification.RecipeName, message);
    }
}
```

### Integrate with PriceChanged (from Week 3):

Update `src/Nastart.Api/Features/Recipes/NotificationHandlers/RecalculateRecipeCostsHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Recipes.NotificationHandlers;

/// <summary>
/// When ingredient price changes, recalculate all affected recipe costs.
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
        var affectedRecipes = await _db.Recipes
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
            
            // Recalculate item costs
            foreach (var item in recipe.Items)
            {
                if (item.IngredientId == notification.IngredientId)
                {
                    item.Cost = item.Quantity * notification.NewPrice;
                }
                else if (item.Ingredient != null)
                {
                    item.Cost = item.Quantity * item.Ingredient.CurrentPrice;
                }
            }
            
            // Recalculate recipe totals
            recipe.TotalCost = recipe.Items.Sum(i => i.Cost);
            
            if (recipe.SellPrice > 0)
            {
                recipe.MarginPercent = 
                    ((recipe.SellPrice - recipe.TotalCost) / recipe.SellPrice) * 100;
            }
            
            recipe.UpdatedAt = DateTime.UtcNow;
            
            _logger.LogInformation(
                "Recipe '{Recipe}': cost {OldCost:N0} → {NewCost:N0}, margin {OldMargin:F1}% → {NewMargin:F1}%",
                recipe.Name, oldCost, recipe.TotalCost, oldMargin, recipe.MarginPercent);
            
            // Check if margin dropped below threshold
            if (oldMargin >= 20 && recipe.MarginPercent < 20)
            {
                await _mediator.Publish(new MarginBelowThresholdNotification(
                    RecipeId: recipe.Id,
                    RecipeName: recipe.Name,
                    NewMargin: recipe.MarginPercent,
                    Threshold: 20,
                    OccurredAt: DateTime.UtcNow
                ), cancellationToken);
            }
            
            // Publish cost changed notification
            await _mediator.Publish(new RecipeCostChangedNotification(
                RecipeId: recipe.Id,
                RecipeName: recipe.Name,
                OldCost: oldCost,
                NewCost: recipe.TotalCost,
                OldMargin: oldMargin,
                NewMargin: recipe.MarginPercent,
                OccurredAt: DateTime.UtcNow
            ), cancellationToken);
        }
        
        await _db.SaveChangesAsync(cancellationToken);
    }
}
```

### Notification Flow Diagram:

```
PriceChangedNotification (from Week 3)
       │
       ▼
RecalculateRecipeCostsHandler
       │
       ├──▶ Updates recipe.TotalCost
       │
       ├──▶ Updates recipe.MarginPercent
       │
       ├──▶ MarginBelowThresholdNotification (if margin < 20%)
       │           │
       │           └──▶ CreateMarginAlertHandler → Creates Alert
       │
       └──▶ RecipeCostChangedNotification
```

### Your Task (Day 6):

1. Create `Features/Recipes/NotificationHandlers/` folder
2. Create `CreateMarginAlertHandler.cs`
3. Create `RecalculateRecipeCostsHandler.cs`
4. Verify build: `dotnet build`

---

# Day 7: Testing Recipe Features

## 🧒 Explain Like I'm 5

Before selling our toys, we test them:
- Can we create a recipe? ✓
- Can we add ingredients? ✓
- Does the cost calculate correctly? ✓
- Does the alert trigger when margin is low? ✓

## 🔧 Engineer Language

Unit tests verify that each recipe feature works correctly. We test handlers, validators, and notification flows.

> 📖 **Microsoft Docs**: *"Unit tests verify the behavior of individual units of code in isolation."*
>
> — [Unit testing in .NET](https://learn.microsoft.com/en-us/dotnet/core/testing/)

### CreateRecipe Handler Tests:

Create `tests/Nastart.Api.Tests/Features/Recipes/CreateRecipeTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Nastart.Api.Features.Recipes;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Tests.Features.Recipes;

public class CreateRecipeTests
{
    private readonly NastartDbContext _db;
    private readonly CreateRecipeHandler _handler;

    public CreateRecipeTests()
    {
        var options = new DbContextOptionsBuilder<NastartDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _db = new NastartDbContext(options);
        var logger = new Mock<ILogger<CreateRecipeHandler>>();
        
        _handler = new CreateRecipeHandler(_db, logger.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesRecipe()
    {
        // Arrange
        var command = new CreateRecipeCommand(
            UserId: Guid.NewGuid(),
            Name: "Chocolate Chip Cookies",
            SellPrice: 25000,
            Description: "Delicious homemade cookies",
            Category: "Cookies",
            YieldQuantity: 24,
            YieldUnit: "pieces"
        );
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Chocolate Chip Cookies");
        result.Value.SellPrice.Should().Be(25000);
        result.Value.TotalCost.Should().Be(0);  // No ingredients yet
        result.Value.MarginPercent.Should().Be(100);  // 100% margin with no costs
        result.Value.Status.Should().Be("Draft");
        
        // Verify saved to database
        var savedRecipe = await _db.Recipes.FirstOrDefaultAsync();
        savedRecipe.Should().NotBeNull();
        savedRecipe!.Name.Should().Be("Chocolate Chip Cookies");
    }

    [Fact]
    public async Task Handle_WithYieldQuantity_SetsCorrectly()
    {
        // Arrange
        var command = new CreateRecipeCommand(
            UserId: Guid.NewGuid(),
            Name: "Banana Bread",
            SellPrice: 50000,
            YieldQuantity: 2,
            YieldUnit: "loaves"
        );
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.YieldQuantity.Should().Be(2);
        result.Value.YieldUnit.Should().Be("loaves");
    }
}
```

### GetRecipeCost Handler Tests:

Create `tests/Nastart.Api.Tests/Features/Recipes/GetRecipeCostTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Features.Recipes;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Tests.Features.Recipes;

public class GetRecipeCostTests
{
    private readonly NastartDbContext _db;
    private readonly GetRecipeCostHandler _handler;

    public GetRecipeCostTests()
    {
        var options = new DbContextOptionsBuilder<NastartDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _db = new NastartDbContext(options);
        _handler = new GetRecipeCostHandler(_db);
    }

    [Fact]
    public async Task Handle_RecipeWithIngredients_CalculatesLiveCost()
    {
        // Arrange
        var userId = Guid.NewGuid();
        
        var flour = new Ingredient
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Flour",
            Unit = "kg",
            CurrentPrice = 20000,  // Current price
            CurrentStock = 10,
            MinimumStock = 2
        };
        
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Bread",
            SellPrice = 50000,
            TotalCost = 10000,  // Old stored cost
            MarginPercent = 80,
            Status = "Active",
            YieldQuantity = 2,
            YieldUnit = "loaves"
        };
        
        var recipeItem = new RecipeItem
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            IngredientId = flour.Id,
            Quantity = 1,  // 1 kg of flour
            Cost = 10000,  // Old stored cost
            Ingredient = flour
        };
        
        recipe.Items.Add(recipeItem);
        
        _db.Ingredients.Add(flour);
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();
        
        // Act
        var result = await _handler.Handle(
            new GetRecipeCostQuery(recipe.Id, userId), 
            CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.LiveTotalCost.Should().Be(20000);  // 1 × 20000
        result.Value.StoredTotalCost.Should().Be(10000);  // Old value
        result.Value.CostDifference.Should().Be(10000);  // Increased!
        result.Value.MarginPercent.Should().Be(60);  // (50000 - 20000) / 50000 × 100
        result.Value.CostPerUnit.Should().Be(10000);  // 20000 / 2 loaves
        result.Value.MarginStatus.Should().Be("Healthy");
    }

    [Fact]
    public async Task Handle_LowMarginRecipe_ReturnsWarningStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        
        var sugar = new Ingredient
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Sugar",
            Unit = "kg",
            CurrentPrice = 45000,  // Expensive!
            CurrentStock = 5,
            MinimumStock = 1
        };
        
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Sugar Cookies",
            SellPrice = 50000,
            TotalCost = 0,
            MarginPercent = 0,
            Status = "Active",
            YieldQuantity = 1,
            YieldUnit = "batch"
        };
        
        var item = new RecipeItem
        {
            Id = Guid.NewGuid(),
            RecipeId = recipe.Id,
            IngredientId = sugar.Id,
            Quantity = 1,
            Cost = 0,
            Ingredient = sugar
        };
        
        recipe.Items.Add(item);
        
        _db.Ingredients.Add(sugar);
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync();
        
        // Act
        var result = await _handler.Handle(
            new GetRecipeCostQuery(recipe.Id, userId), 
            CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.MarginPercent.Should().Be(10);  // (50000 - 45000) / 50000 × 100
        result.Value.MarginStatus.Should().Be("Critical");  // < 10%
    }

    [Fact]
    public async Task Handle_RecipeNotFound_ReturnsFailure()
    {
        // Act
        var result = await _handler.Handle(
            new GetRecipeCostQuery(Guid.NewGuid(), Guid.NewGuid()), 
            CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("RECIPE_NOT_FOUND");
    }
}
```

### Validator Tests:

Create `tests/Nastart.Api.Tests/Features/Recipes/CreateRecipeValidatorTests.cs`:

```csharp
using FluentAssertions;
using Nastart.Api.Features.Recipes;

namespace Nastart.Api.Tests.Features.Recipes;

public class CreateRecipeValidatorTests
{
    private readonly CreateRecipeValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_PassesValidation()
    {
        // Arrange
        var command = new CreateRecipeCommand(
            UserId: Guid.NewGuid(),
            Name: "Test Recipe",
            SellPrice: 10000
        );
        
        // Act
        var result = _validator.Validate(command);
        
        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyName_FailsValidation()
    {
        // Arrange
        var command = new CreateRecipeCommand(
            UserId: Guid.NewGuid(),
            Name: "",
            SellPrice: 10000
        );
        
        // Act
        var result = _validator.Validate(command);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_ZeroSellPrice_FailsValidation()
    {
        // Arrange
        var command = new CreateRecipeCommand(
            UserId: Guid.NewGuid(),
            Name: "Test",
            SellPrice: 0
        );
        
        // Act
        var result = _validator.Validate(command);
        
        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SellPrice");
    }
}
```

### Run Tests:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend

dotnet test --filter "FullyQualifiedName~Recipes"
```

### Your Task (Day 7):

1. Create test files for recipe features
2. Run all tests with `dotnet test`
3. Ensure >80% coverage on handlers

---

# Resources

## Microsoft Official Documentation

| Topic | Link |
|-------|------|
| **EF Core Modeling** | [learn.microsoft.com/ef/core/modeling](https://learn.microsoft.com/en-us/ef/core/modeling/) |
| **Loading Related Data** | [learn.microsoft.com/ef/core/querying/related-data](https://learn.microsoft.com/en-us/ef/core/querying/related-data) |
| **Minimal APIs** | [learn.microsoft.com/aspnet/core/fundamentals/minimal-apis](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview) |
| **MediatR Notifications** | [learn.microsoft.com/dotnet/architecture/microservices](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api) |
| **Domain Events** | [learn.microsoft.com/dotnet/architecture/microservices](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation) |
| **Unit Testing** | [learn.microsoft.com/dotnet/core/testing](https://learn.microsoft.com/en-us/dotnet/core/testing/) |

## Week 4 Checklist

- [ ] Created `Recipe` and `RecipeItem` entity models
- [ ] Created `CreateRecipe` feature slice with validation
- [ ] Created `AddIngredientToRecipe` feature
- [ ] Created `RemoveIngredientFromRecipe` feature
- [ ] Created `GetRecipeCost` query with live calculation
- [ ] Created `GetRecipes` list query
- [ ] Created `RecipeNotifications.cs` with notification records
- [ ] Created `UpdateRecipeSellPrice` feature
- [ ] Created `CreateMarginAlertHandler` notification handler
- [ ] Created `RecalculateRecipeCostsHandler` for price changes
- [ ] Unit tests for recipe handlers
- [ ] All builds pass: `dotnet build`
- [ ] All tests pass: `dotnet test`

---

## What's Next?

**Week 5: Database & Shared Services** — Configure EF Core mappings, create migrations, seed data, and build the `Result<T>` pattern with validation pipeline behaviors.

---

*Nastart — Start smart, bake profitable*
