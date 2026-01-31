# Week 5: Application Layer with CQRS 🎯

> **Goal**: Build the Application Layer using MediatR for CQRS — Commands for state changes, Queries for reads, Validators for input validation, and Domain Event Handlers for side effects.

---

## Table of Contents
1. [Day 1: MediatR Setup & Project Structure](#day-1-mediatr-setup--project-structure)
2. [Day 2: Commands & Command Handlers](#day-2-commands--command-handlers)
3. [Day 3: Queries & Query Handlers](#day-3-queries--query-handlers)
4. [Day 4: FluentValidation & Pipeline Behaviors](#day-4-fluentvalidation--pipeline-behaviors)
5. [Day 5: Domain Event Handlers](#day-5-domain-event-handlers)
6. [Day 6: DTOs & Result Pattern](#day-6-dtos--result-pattern)
7. [Day 7: Integration Testing Application Layer](#day-7-integration-testing-application-layer)
8. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1: MediatR Setup & Project Structure

## 🧒 Explain Like I'm 5

Imagine you're in a big restaurant 🍕. When you want pizza, you don't go to the kitchen yourself. You tell the waiter, and the waiter tells the cook.

**MediatR** is like that waiter! You send a "request" (like ordering pizza), and MediatR finds the right "handler" (like the cook) to do the work.

## 🔧 Engineer Language

**MediatR** implements the Mediator pattern — it decouples the sender of a request from the handler. This enables:
- **Single Responsibility**: Controllers just receive requests and send responses
- **Testability**: Handlers can be tested in isolation
- **Pipeline Behaviors**: Cross-cutting concerns (logging, validation) applied consistently

> 📖 **Microsoft Docs**: *"Using the Mediator pattern helps you to reduce coupling and to isolate the concerns of the requested work, while automatically connecting to the handler that performs that work—in this case, to command handlers."*
>
> — [Implement the microservice application layer using the Web API](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api)

### What is CQRS?

**Command Query Responsibility Segregation** separates:

| Aspect | Commands (Write) | Queries (Read) |
|--------|-----------------|----------------|
| Purpose | Change state | Return data |
| Side effects | Yes | No (idempotent) |
| Return | Success/Failure | Data/DTO |
| DDD patterns | Full aggregate rules | Can bypass aggregates |
| Example | `CreateRecipeCommand` | `GetRecipeByIdQuery` |

> 📖 **Microsoft Docs**: *"CQRS is an architectural pattern that separates the models for reading and writing data. Queries return a result and don't change the state of the system. Commands change the state of a system."*
>
> — [Apply simplified CQRS and DDD patterns](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/apply-simplified-microservice-cqrs-ddd-patterns)

### Install Required Packages

```powershell
cd c:\Users\AU1833\Documents\personal\nastart\backend

# MediatR for mediator pattern
dotnet add src/Nastart.Application/Nastart.Application.csproj package MediatR

# FluentValidation for command validation
dotnet add src/Nastart.Application/Nastart.Application.csproj package FluentValidation
dotnet add src/Nastart.Application/Nastart.Application.csproj package FluentValidation.DependencyInjectionExtensions

# For API layer (later)
dotnet add src/Nastart.Api/Nastart.Api.csproj package MediatR
```

### Application Layer Project Structure

```
src/Nastart.Application/
├── Common/
│   ├── Behaviors/
│   │   ├── LoggingBehavior.cs          # Logs all requests
│   │   ├── ValidationBehavior.cs       # Validates commands
│   │   └── PerformanceBehavior.cs      # Tracks slow requests
│   ├── Exceptions/
│   │   ├── ValidationException.cs
│   │   └── NotFoundException.cs
│   ├── Interfaces/
│   │   ├── IApplicationDbContext.cs    # Abstraction for DbContext
│   │   └── ICurrentUserService.cs      # Get current user
│   └── Models/
│       └── Result.cs                   # Result pattern
│
├── Inventory/
│   ├── Commands/
│   │   ├── CreateIngredient/
│   │   │   ├── CreateIngredientCommand.cs
│   │   │   ├── CreateIngredientCommandHandler.cs
│   │   │   └── CreateIngredientCommandValidator.cs
│   │   └── UpdateIngredient/
│   │       └── ...
│   ├── Queries/
│   │   ├── GetIngredientById/
│   │   │   ├── GetIngredientByIdQuery.cs
│   │   │   ├── GetIngredientByIdQueryHandler.cs
│   │   │   └── IngredientDto.cs
│   │   └── GetLowStockIngredients/
│   │       └── ...
│   └── EventHandlers/
│       └── PriceChangedEventHandler.cs
│
├── Recipe/
│   ├── Commands/
│   │   ├── CreateRecipe/
│   │   ├── AddIngredientToRecipe/
│   │   └── UpdateRecipePrice/
│   ├── Queries/
│   │   ├── GetRecipeById/
│   │   ├── GetRecipeCost/
│   │   └── GetLowMarginRecipes/
│   └── EventHandlers/
│       └── MarginBelowThresholdEventHandler.cs
│
├── Finance/
│   ├── Commands/
│   │   └── RecordPurchase/
│   ├── Queries/
│   │   └── GetPriceHistory/
│   └── EventHandlers/
│       └── PriceSpikeDetectedEventHandler.cs
│
└── DependencyInjection.cs              # Service registration
```

### Application Layer Dependencies

The Application layer should reference:
- `Nastart.Domain` — For domain entities, aggregates, events
- MediatR — For request/response handling
- FluentValidation — For command validation

Update `src/Nastart.Application/Nastart.Application.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MediatR" Version="12.*" />
    <PackageReference Include="FluentValidation" Version="11.*" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Nastart.Domain\Nastart.Domain.csproj" />
  </ItemGroup>

</Project>
```

### Dependency Injection Setup

Create `src/Nastart.Application/DependencyInjection.cs`:

```csharp
using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Nastart.Application.Common.Behaviors;

namespace Nastart.Application;

/// <summary>
/// Extension methods for registering Application layer services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Application layer services to the DI container.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR handlers from this assembly
        services.AddMediatR(cfg => 
        {
            cfg.RegisterServicesFromAssembly(assembly);
            
            // Add pipeline behaviors in order
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        });

        // Register all FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
```

### In Program.cs (API Layer):

```csharp
// Program.cs — Preview for later
using Nastart.Application;

var builder = WebApplication.CreateBuilder(args);

// Add Application layer services
builder.Services.AddApplicationServices();

// ... rest of configuration
```

---

# Day 2: Commands & Command Handlers

## 🧒 Explain Like I'm 5

A **command** is like a sticky note 📝 that says "Please do this thing!"
- "Create a new recipe called Pizza"
- "Add flour to the recipe"

A **command handler** is like a helper who reads the sticky note and actually does the work.

## 🔧 Engineer Language

**Commands** represent intent to change state. They are:
- **Immutable** — Data set at construction, never changed
- **Explicit** — Named after the action (`CreateRecipeCommand`, not `RecipeCommand`)
- **Processed once** — Unlike events, commands target a single handler

> 📖 **Microsoft Docs**: *"A command is implemented with a class that contains data fields or collections with all the information that is needed in order to execute that command. A command is a special kind of Data Transfer Object (DTO), one that is specifically used to request changes or transactions."*
>
> — [Implement the Command and Command Handler patterns](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api)

### Command Naming Convention

```
[Action][Entity]Command
```

Examples:
- `CreateRecipeCommand`
- `AddIngredientToRecipeCommand`
- `RecordPurchaseCommand`
- `UpdateRecipePriceCommand`
- `DeactivateRecipeCommand`

### CreateRecipeCommand

Create `src/Nastart.Application/Recipe/Commands/CreateRecipe/CreateRecipeCommand.cs`:

```csharp
using MediatR;
using Nastart.Application.Common.Models;

namespace Nastart.Application.Recipe.Commands.CreateRecipe;

/// <summary>
/// Command to create a new recipe.
/// Immutable — all properties set via constructor.
/// </summary>
/// <remarks>
/// Following DDD and CQRS patterns, commands should be immutable.
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api
/// </remarks>
public sealed record CreateRecipeCommand : IRequest<Result<Guid>>
{
    /// <summary>The name of the recipe (e.g., "Margherita Pizza").</summary>
    public required string Name { get; init; }
    
    /// <summary>The category (e.g., "Main Course", "Dessert").</summary>
    public required string Category { get; init; }
    
    /// <summary>The selling price of this recipe.</summary>
    public required decimal SellingPrice { get; init; }
    
    /// <summary>Optional description of the recipe.</summary>
    public string? Description { get; init; }
    
    /// <summary>Optional initial ingredients to add.</summary>
    public IReadOnlyList<RecipeIngredientDto>? InitialIngredients { get; init; }
}

/// <summary>
/// DTO for initial ingredients when creating a recipe.
/// </summary>
public sealed record RecipeIngredientDto
{
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required decimal UnitCost { get; init; }
}
```

### CreateRecipeCommandHandler

Create `src/Nastart.Application/Recipe/Commands/CreateRecipe/CreateRecipeCommandHandler.cs`:

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using Nastart.Application.Common.Interfaces;
using Nastart.Application.Common.Models;
using Nastart.Domain.Inventory.ValueObjects;
using Nastart.Domain.Recipe.Aggregates;
using Nastart.Domain.Recipe.ValueObjects;

namespace Nastart.Application.Recipe.Commands.CreateRecipe;

/// <summary>
/// Handles the CreateRecipeCommand by creating a new Recipe aggregate.
/// </summary>
/// <remarks>
/// Command handler responsibilities:
/// 1. Receive command object
/// 2. Validate command (via pipeline behavior)
/// 3. Create/load aggregate
/// 4. Execute domain methods
/// 5. Persist changes
/// 
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api
/// </remarks>
public sealed class CreateRecipeCommandHandler 
    : IRequestHandler<CreateRecipeCommand, Result<Guid>>
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateRecipeCommandHandler> _logger;

    public CreateRecipeCommandHandler(
        IRecipeRepository recipeRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateRecipeCommandHandler> logger)
    {
        _recipeRepository = recipeRepository ?? throw new ArgumentNullException(nameof(recipeRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<Guid>> Handle(
        CreateRecipeCommand request, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating recipe: {RecipeName} in category {Category}",
            request.Name,
            request.Category);

        try
        {
            // 1. Create the aggregate using factory method
            var recipe = Domain.Recipe.Aggregates.Recipe.Create(
                name: request.Name,
                sellingPrice: request.SellingPrice,
                category: request.Category,
                description: request.Description
            );

            // 2. Add initial ingredients if provided
            if (request.InitialIngredients is { Count: > 0 })
            {
                foreach (var ingredient in request.InitialIngredients)
                {
                    recipe.AddIngredient(
                        ingredientId: new IngredientId(ingredient.IngredientId),
                        ingredientName: ingredient.IngredientName,
                        quantity: ingredient.Quantity,
                        unit: ingredient.Unit,
                        unitCost: ingredient.UnitCost
                    );
                }
            }

            // 3. Add to repository
            await _recipeRepository.AddAsync(recipe, cancellationToken);

            // 4. Save changes (commits transaction, dispatches domain events)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Recipe created successfully: {RecipeId} - {RecipeName}",
                recipe.Id,
                recipe.Name);

            // 5. Return success with the new ID
            return Result<Guid>.Success(recipe.Id.Value);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error creating recipe: {Message}", ex.Message);
            return Result<Guid>.Failure($"Validation error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating recipe: {RecipeName}", request.Name);
            return Result<Guid>.Failure("An error occurred while creating the recipe.");
        }
    }
}
```

### AddIngredientToRecipeCommand

Create `src/Nastart.Application/Recipe/Commands/AddIngredientToRecipe/AddIngredientToRecipeCommand.cs`:

```csharp
using MediatR;
using Nastart.Application.Common.Models;

namespace Nastart.Application.Recipe.Commands.AddIngredientToRecipe;

/// <summary>
/// Command to add an ingredient to an existing recipe.
/// </summary>
public sealed record AddIngredientToRecipeCommand : IRequest<Result>
{
    /// <summary>The recipe to add the ingredient to.</summary>
    public required Guid RecipeId { get; init; }
    
    /// <summary>The ingredient to add.</summary>
    public required Guid IngredientId { get; init; }
    
    /// <summary>Display name of the ingredient.</summary>
    public required string IngredientName { get; init; }
    
    /// <summary>Quantity needed for the recipe.</summary>
    public required decimal Quantity { get; init; }
    
    /// <summary>Unit of measurement.</summary>
    public required string Unit { get; init; }
    
    /// <summary>Current cost per unit.</summary>
    public required decimal UnitCost { get; init; }
}
```

Create `src/Nastart.Application/Recipe/Commands/AddIngredientToRecipe/AddIngredientToRecipeCommandHandler.cs`:

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using Nastart.Application.Common.Exceptions;
using Nastart.Application.Common.Interfaces;
using Nastart.Application.Common.Models;
using Nastart.Domain.Inventory.ValueObjects;
using Nastart.Domain.Recipe.ValueObjects;

namespace Nastart.Application.Recipe.Commands.AddIngredientToRecipe;

/// <summary>
/// Handles adding an ingredient to an existing recipe.
/// Demonstrates the pattern of loading an aggregate, calling domain methods, and saving.
/// </summary>
public sealed class AddIngredientToRecipeCommandHandler 
    : IRequestHandler<AddIngredientToRecipeCommand, Result>
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddIngredientToRecipeCommandHandler> _logger;

    public AddIngredientToRecipeCommandHandler(
        IRecipeRepository recipeRepository,
        IUnitOfWork unitOfWork,
        ILogger<AddIngredientToRecipeCommandHandler> logger)
    {
        _recipeRepository = recipeRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(
        AddIngredientToRecipeCommand request, 
        CancellationToken cancellationToken)
    {
        // 1. Load the aggregate
        var recipeId = new RecipeId(request.RecipeId);
        var recipe = await _recipeRepository.GetByIdAsync(recipeId, cancellationToken);
        
        if (recipe is null)
        {
            _logger.LogWarning("Recipe not found: {RecipeId}", request.RecipeId);
            throw new NotFoundException(nameof(Recipe), request.RecipeId);
        }

        // 2. Call domain method (business logic stays in aggregate)
        recipe.AddIngredient(
            ingredientId: new IngredientId(request.IngredientId),
            ingredientName: request.IngredientName,
            quantity: request.Quantity,
            unit: request.Unit,
            unitCost: request.UnitCost
        );

        // 3. Update in repository
        _recipeRepository.Update(recipe);

        // 4. Save changes
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Added ingredient {IngredientName} to recipe {RecipeId}",
            request.IngredientName,
            request.RecipeId);

        return Result.Success();
    }
}
```

### RecordPurchaseCommand (Finance Context)

Create `src/Nastart.Application/Finance/Commands/RecordPurchase/RecordPurchaseCommand.cs`:

```csharp
using MediatR;
using Nastart.Application.Common.Models;

namespace Nastart.Application.Finance.Commands.RecordPurchase;

/// <summary>
/// Command to record a new purchase from a supplier.
/// Triggers price analysis and potential spike detection.
/// </summary>
public sealed record RecordPurchaseCommand : IRequest<Result<Guid>>
{
    /// <summary>The shop/supplier where the purchase was made.</summary>
    public required Guid ShopId { get; init; }
    
    /// <summary>Date of the purchase.</summary>
    public required DateOnly PurchaseDate { get; init; }
    
    /// <summary>Optional receipt image URL.</summary>
    public string? ReceiptImageUrl { get; init; }
    
    /// <summary>Notes about this purchase.</summary>
    public string? Notes { get; init; }
    
    /// <summary>Items purchased.</summary>
    public required IReadOnlyList<PurchaseItemDto> Items { get; init; }
}

/// <summary>
/// DTO for a single purchase item.
/// </summary>
public sealed record PurchaseItemDto
{
    public required Guid IngredientId { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
}
```

Create `src/Nastart.Application/Finance/Commands/RecordPurchase/RecordPurchaseCommandHandler.cs`:

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using Nastart.Application.Common.Interfaces;
using Nastart.Application.Common.Models;
using Nastart.Domain.Finance.Aggregates;
using Nastart.Domain.Finance.Services;
using Nastart.Domain.Finance.ValueObjects;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Application.Finance.Commands.RecordPurchase;

/// <summary>
/// Handles recording a purchase and detecting price spikes.
/// </summary>
public sealed class RecordPurchaseCommandHandler 
    : IRequestHandler<RecordPurchaseCommand, Result<Guid>>
{
    private readonly IPurchaseRepository _purchaseRepository;
    private readonly IPriceHistoryRepository _priceHistoryRepository;
    private readonly IPriceAnalysisService _priceAnalysisService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RecordPurchaseCommandHandler> _logger;

    public RecordPurchaseCommandHandler(
        IPurchaseRepository purchaseRepository,
        IPriceHistoryRepository priceHistoryRepository,
        IPriceAnalysisService priceAnalysisService,
        IUnitOfWork unitOfWork,
        ILogger<RecordPurchaseCommandHandler> logger)
    {
        _purchaseRepository = purchaseRepository;
        _priceHistoryRepository = priceHistoryRepository;
        _priceAnalysisService = priceAnalysisService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(
        RecordPurchaseCommand request, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Recording purchase with {ItemCount} items from shop {ShopId}",
            request.Items.Count,
            request.ShopId);

        // 1. Create the Purchase aggregate
        var purchase = new Purchase(
            id: PurchaseId.New(),
            shopId: new ShopId(request.ShopId),
            purchaseDate: request.PurchaseDate.ToDateTime(TimeOnly.MinValue),
            receiptImageUrl: request.ReceiptImageUrl
        );

        // 2. Add items and check for price spikes
        foreach (var item in request.Items)
        {
            var ingredientId = new IngredientId(item.IngredientId);
            
            // Add item to purchase
            purchase.AddItem(ingredientId, item.Quantity, item.UnitPrice);
            
            // Get price history to check for spikes
            var priceHistory = await _priceHistoryRepository
                .GetByIngredientIdAsync(ingredientId, cancellationToken);
            
            if (priceHistory is not null)
            {
                // Use domain service to detect price spike
                var spikeEvent = _priceAnalysisService.AnalyzePriceChange(
                    priceHistory,
                    item.UnitPrice
                );
                
                // If spike detected, the domain service returns an event
                // The aggregate can also raise this internally
            }
        }

        // 3. Persist
        await _purchaseRepository.AddAsync(purchase, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Purchase recorded: {PurchaseId}, Total: {Total}",
            purchase.Id,
            purchase.Total);

        return Result<Guid>.Success(purchase.Id.Value);
    }
}
```

### Repository Interfaces (in Domain Layer)

Create `src/Nastart.Domain/Recipe/IRecipeRepository.cs`:

```csharp
using Nastart.Domain.Recipe.Aggregates;
using Nastart.Domain.Recipe.ValueObjects;

namespace Nastart.Domain.Recipe;

/// <summary>
/// Repository interface for Recipe aggregate.
/// Defined in Domain layer, implemented in Infrastructure layer.
/// </summary>
public interface IRecipeRepository
{
    Task<Aggregates.Recipe?> GetByIdAsync(RecipeId id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Aggregates.Recipe>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Aggregates.Recipe recipe, CancellationToken cancellationToken = default);
    void Update(Aggregates.Recipe recipe);
    void Delete(Aggregates.Recipe recipe);
}
```

---

# Day 3: Queries & Query Handlers

## 🧒 Explain Like I'm 5

Queries are like asking questions 🙋:
- "What's in this recipe?"
- "Which recipes have low profit margins?"
- "What's the price of flour?"

The answers don't change anything — they just tell you information!

## 🔧 Engineer Language

**Queries** are idempotent operations that return data without side effects. In CQRS, queries:
- Don't use aggregates (can query directly from database)
- Return DTOs/ViewModels tailored for the client
- Can use simpler data access (Dapper, raw SQL, projections)

> 📖 **Microsoft Docs**: *"For reads/queries, the ordering microservice implements the queries independently from the DDD model and transactional area. This implementation was done primarily because the demands for queries and for transactions are drastically different."*
>
> — [Implement reads/queries in a CQRS microservice](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/cqrs-microservice-reads)

### Query Naming Convention

```
Get[What]Query
Get[What]By[Criteria]Query
```

Examples:
- `GetRecipeByIdQuery`
- `GetLowMarginRecipesQuery`
- `GetIngredientPriceHistoryQuery`
- `GetDashboardSummaryQuery`

### GetRecipeByIdQuery

Create `src/Nastart.Application/Recipe/Queries/GetRecipeById/GetRecipeByIdQuery.cs`:

```csharp
using MediatR;

namespace Nastart.Application.Recipe.Queries.GetRecipeById;

/// <summary>
/// Query to get a recipe by its ID.
/// Returns a DTO, not the domain entity.
/// </summary>
public sealed record GetRecipeByIdQuery(Guid RecipeId) : IRequest<RecipeDetailsDto?>;
```

Create `src/Nastart.Application/Recipe/Queries/GetRecipeById/RecipeDetailsDto.cs`:

```csharp
namespace Nastart.Application.Recipe.Queries.GetRecipeById;

/// <summary>
/// DTO for recipe details.
/// Tailored for API response, not tied to domain model.
/// </summary>
/// <remarks>
/// Using C# record types for concise, immutable DTOs.
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/cqrs-microservice-reads
/// </remarks>
public sealed record RecipeDetailsDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public string? Description { get; init; }
    public required decimal SellingPrice { get; init; }
    public required decimal TotalCost { get; init; }
    public required decimal MarginPercentage { get; init; }
    public required decimal MarginAmount { get; init; }
    public required string MarginStatus { get; init; }
    public required bool IsActive { get; init; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? ModifiedAt { get; init; }
    public required IReadOnlyList<RecipeItemDto> Items { get; init; }
}

/// <summary>
/// DTO for a recipe item (ingredient in recipe).
/// </summary>
public sealed record RecipeItemDto
{
    public required Guid Id { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required decimal UnitCost { get; init; }
    public required decimal TotalCost { get; init; }
    public required decimal CostPercentage { get; init; }
}
```

Create `src/Nastart.Application/Recipe/Queries/GetRecipeById/GetRecipeByIdQueryHandler.cs`:

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Recipe.Queries.GetRecipeById;

/// <summary>
/// Handles the GetRecipeByIdQuery.
/// Uses the query interface for optimized reads.
/// </summary>
public sealed class GetRecipeByIdQueryHandler 
    : IRequestHandler<GetRecipeByIdQuery, RecipeDetailsDto?>
{
    private readonly IRecipeQueries _recipeQueries;
    private readonly ILogger<GetRecipeByIdQueryHandler> _logger;

    public GetRecipeByIdQueryHandler(
        IRecipeQueries recipeQueries,
        ILogger<GetRecipeByIdQueryHandler> logger)
    {
        _recipeQueries = recipeQueries;
        _logger = logger;
    }

    public async Task<RecipeDetailsDto?> Handle(
        GetRecipeByIdQuery request, 
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting recipe by ID: {RecipeId}", request.RecipeId);
        
        var recipe = await _recipeQueries.GetByIdAsync(request.RecipeId, cancellationToken);
        
        if (recipe is null)
        {
            _logger.LogDebug("Recipe not found: {RecipeId}", request.RecipeId);
        }
        
        return recipe;
    }
}
```

### Query Interface (for optimized reads)

Create `src/Nastart.Application/Common/Interfaces/IRecipeQueries.cs`:

```csharp
using Nastart.Application.Recipe.Queries.GetRecipeById;
using Nastart.Application.Recipe.Queries.GetLowMarginRecipes;

namespace Nastart.Application.Common.Interfaces;

/// <summary>
/// Query interface for recipes.
/// Implemented in Infrastructure layer, can use Dapper, EF projections, etc.
/// </summary>
/// <remarks>
/// Queries don't go through aggregates — they read directly from the database
/// for performance. This is the "Q" in CQRS.
/// 
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/cqrs-microservice-reads
/// </remarks>
public interface IRecipeQueries
{
    Task<RecipeDetailsDto?> GetByIdAsync(Guid recipeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecipeSummaryDto>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LowMarginRecipeDto>> GetLowMarginRecipesAsync(decimal threshold, CancellationToken cancellationToken = default);
}
```

### GetLowMarginRecipesQuery

Create `src/Nastart.Application/Recipe/Queries/GetLowMarginRecipes/GetLowMarginRecipesQuery.cs`:

```csharp
using MediatR;

namespace Nastart.Application.Recipe.Queries.GetLowMarginRecipes;

/// <summary>
/// Query to get recipes with margins below a threshold.
/// </summary>
/// <param name="ThresholdPercentage">Minimum acceptable margin (default 15%).</param>
public sealed record GetLowMarginRecipesQuery(decimal ThresholdPercentage = 15m) 
    : IRequest<IReadOnlyList<LowMarginRecipeDto>>;
```

Create `src/Nastart.Application/Recipe/Queries/GetLowMarginRecipes/LowMarginRecipeDto.cs`:

```csharp
namespace Nastart.Application.Recipe.Queries.GetLowMarginRecipes;

/// <summary>
/// DTO for low margin recipe alerts.
/// </summary>
public sealed record LowMarginRecipeDto
{
    public required Guid RecipeId { get; init; }
    public required string RecipeName { get; init; }
    public required string Category { get; init; }
    public required decimal SellingPrice { get; init; }
    public required decimal TotalCost { get; init; }
    public required decimal MarginPercentage { get; init; }
    public required decimal SuggestedPrice { get; init; }
    public required string Severity { get; init; }  // "Warning", "Critical", "Loss"
}
```

Create `src/Nastart.Application/Recipe/Queries/GetLowMarginRecipes/GetLowMarginRecipesQueryHandler.cs`:

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Application.Recipe.Queries.GetLowMarginRecipes;

/// <summary>
/// Handles the GetLowMarginRecipesQuery.
/// </summary>
public sealed class GetLowMarginRecipesQueryHandler 
    : IRequestHandler<GetLowMarginRecipesQuery, IReadOnlyList<LowMarginRecipeDto>>
{
    private readonly IRecipeQueries _recipeQueries;
    private readonly ILogger<GetLowMarginRecipesQueryHandler> _logger;

    public GetLowMarginRecipesQueryHandler(
        IRecipeQueries recipeQueries,
        ILogger<GetLowMarginRecipesQueryHandler> logger)
    {
        _recipeQueries = recipeQueries;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LowMarginRecipeDto>> Handle(
        GetLowMarginRecipesQuery request, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Getting recipes with margin below {Threshold}%",
            request.ThresholdPercentage);
        
        var recipes = await _recipeQueries.GetLowMarginRecipesAsync(
            request.ThresholdPercentage, 
            cancellationToken);
        
        _logger.LogInformation("Found {Count} low margin recipes", recipes.Count);
        
        return recipes;
    }
}
```

### GetDashboardSummaryQuery

Create `src/Nastart.Application/Common/Queries/GetDashboardSummary/GetDashboardSummaryQuery.cs`:

```csharp
using MediatR;

namespace Nastart.Application.Common.Queries.GetDashboardSummary;

/// <summary>
/// Query to get dashboard summary data.
/// Aggregates data from multiple contexts.
/// </summary>
public sealed record GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>;

/// <summary>
/// Dashboard summary DTO.
/// </summary>
public sealed record DashboardSummaryDto
{
    public required int TotalRecipes { get; init; }
    public required int ActiveRecipes { get; init; }
    public required int LowMarginRecipes { get; init; }
    public required int TotalIngredients { get; init; }
    public required int LowStockIngredients { get; init; }
    public required int ActiveAlerts { get; init; }
    public required decimal TotalRevenue { get; init; }
    public required decimal TotalCost { get; init; }
    public required decimal OverallMargin { get; init; }
    public required IReadOnlyList<RecentPurchaseDto> RecentPurchases { get; init; }
    public required IReadOnlyList<TopRecipeDto> TopRecipes { get; init; }
}

public sealed record RecentPurchaseDto
{
    public required Guid Id { get; init; }
    public required DateTime Date { get; init; }
    public required string ShopName { get; init; }
    public required decimal Total { get; init; }
    public required int ItemCount { get; init; }
}

public sealed record TopRecipeDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required decimal MarginPercentage { get; init; }
    public required int SalesCount { get; init; }
}
```

---

# Day 4: FluentValidation & Pipeline Behaviors

## 🧒 Explain Like I'm 5

Before you bake a cake 🎂, you check if you have all the ingredients and they're good.

**Validation** is checking if the request is okay before we do the work:
- "Did they give us a recipe name?"
- "Is the price a positive number?"

**Pipeline Behaviors** are like checkpoints that every request goes through!

## 🔧 Engineer Language

**Pipeline Behaviors** in MediatR are middleware that wrap around handlers. They execute in order for every request, enabling cross-cutting concerns:

1. **LoggingBehavior** — Logs request/response
2. **ValidationBehavior** — Validates commands using FluentValidation
3. **PerformanceBehavior** — Logs slow requests

> 📖 **Microsoft Docs**: *"The guide proposes applying behaviors in order to separate cross-cutting concerns."*
>
> — [Implement the command process pipeline with a mediator pattern (MediatR)](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api)

### Validation Behavior

Create `src/Nastart.Application/Common/Behaviors/ValidationBehavior.cs`:

```csharp
using FluentValidation;
using MediatR;
using Nastart.Application.Common.Exceptions;

namespace Nastart.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that validates requests using FluentValidation.
/// Runs before the handler and throws if validation fails.
/// </summary>
/// <remarks>
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api#apply-cross-cutting-concerns-when-processing-commands-with-the-behaviors-in-mediatr
/// </remarks>
public sealed class ValidationBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        // Run all validators
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        // Collect all failures
        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
```

### Logging Behavior

Create `src/Nastart.Application/Common/Behaviors/LoggingBehavior.cs`:

```csharp
using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Nastart.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs all requests and responses.
/// </summary>
/// <remarks>
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api#apply-cross-cutting-concerns-when-processing-commands-with-the-behaviors-in-mediatr
/// </remarks>
public sealed class LoggingBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        
        _logger.LogInformation(
            "[START] Handling {RequestName}: {@Request}",
            requestName,
            request);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            
            stopwatch.Stop();
            
            _logger.LogInformation(
                "[END] Handled {RequestName} in {ElapsedMs}ms: {@Response}",
                requestName,
                stopwatch.ElapsedMilliseconds,
                response);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(
                ex,
                "[ERROR] {RequestName} failed after {ElapsedMs}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);
            
            throw;
        }
    }
}
```

### Performance Behavior

Create `src/Nastart.Application/Common/Behaviors/PerformanceBehavior.cs`:

```csharp
using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Nastart.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs warnings for slow requests.
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private const int SlowRequestThresholdMs = 500;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request, 
        RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
        {
            var requestName = typeof(TRequest).Name;
            
            _logger.LogWarning(
                "[PERFORMANCE] {RequestName} took {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                requestName,
                stopwatch.ElapsedMilliseconds,
                SlowRequestThresholdMs);
        }

        return response;
    }
}
```

### CreateRecipeCommandValidator

Create `src/Nastart.Application/Recipe/Commands/CreateRecipe/CreateRecipeCommandValidator.cs`:

```csharp
using FluentValidation;

namespace Nastart.Application.Recipe.Commands.CreateRecipe;

/// <summary>
/// Validator for CreateRecipeCommand.
/// Uses FluentValidation for declarative, testable validation rules.
/// </summary>
/// <remarks>
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api#apply-cross-cutting-concerns-when-processing-commands-with-the-behaviors-in-mediatr
/// </remarks>
public sealed class CreateRecipeCommandValidator : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Recipe name is required.")
            .MaximumLength(200)
            .WithMessage("Recipe name cannot exceed 200 characters.");

        RuleFor(x => x.Category)
            .NotEmpty()
            .WithMessage("Category is required.")
            .MaximumLength(100)
            .WithMessage("Category cannot exceed 100 characters.");

        RuleFor(x => x.SellingPrice)
            .GreaterThan(0)
            .WithMessage("Selling price must be greater than zero.");

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .WithMessage("Description cannot exceed 2000 characters.")
            .When(x => x.Description is not null);

        // Validate initial ingredients if provided
        RuleForEach(x => x.InitialIngredients)
            .ChildRules(ingredient =>
            {
                ingredient.RuleFor(i => i.IngredientId)
                    .NotEmpty()
                    .WithMessage("Ingredient ID is required.");
                
                ingredient.RuleFor(i => i.IngredientName)
                    .NotEmpty()
                    .WithMessage("Ingredient name is required.");
                
                ingredient.RuleFor(i => i.Quantity)
                    .GreaterThan(0)
                    .WithMessage("Quantity must be greater than zero.");
                
                ingredient.RuleFor(i => i.Unit)
                    .NotEmpty()
                    .WithMessage("Unit is required.");
                
                ingredient.RuleFor(i => i.UnitCost)
                    .GreaterThanOrEqualTo(0)
                    .WithMessage("Unit cost cannot be negative.");
            })
            .When(x => x.InitialIngredients is not null);
    }
}
```

### AddIngredientToRecipeCommandValidator

Create `src/Nastart.Application/Recipe/Commands/AddIngredientToRecipe/AddIngredientToRecipeCommandValidator.cs`:

```csharp
using FluentValidation;

namespace Nastart.Application.Recipe.Commands.AddIngredientToRecipe;

/// <summary>
/// Validator for AddIngredientToRecipeCommand.
/// </summary>
public sealed class AddIngredientToRecipeCommandValidator 
    : AbstractValidator<AddIngredientToRecipeCommand>
{
    public AddIngredientToRecipeCommandValidator()
    {
        RuleFor(x => x.RecipeId)
            .NotEmpty()
            .WithMessage("Recipe ID is required.");

        RuleFor(x => x.IngredientId)
            .NotEmpty()
            .WithMessage("Ingredient ID is required.");

        RuleFor(x => x.IngredientName)
            .NotEmpty()
            .WithMessage("Ingredient name is required.")
            .MaximumLength(200)
            .WithMessage("Ingredient name cannot exceed 200 characters.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than zero.");

        RuleFor(x => x.Unit)
            .NotEmpty()
            .WithMessage("Unit is required.");

        RuleFor(x => x.UnitCost)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Unit cost cannot be negative.");
    }
}
```

### Custom Validation Exception

Create `src/Nastart.Application/Common/Exceptions/ValidationException.cs`:

```csharp
using FluentValidation.Results;

namespace Nastart.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when command validation fails.
/// Contains all validation errors for the request.
/// </summary>
public sealed class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException()
        : base("One or more validation failures have occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : this()
    {
        Errors = failures
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(
                failureGroup => failureGroup.Key,
                failureGroup => failureGroup.ToArray());
    }
}
```

### NotFoundException

Create `src/Nastart.Application/Common/Exceptions/NotFoundException.cs`:

```csharp
namespace Nastart.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when a requested entity is not found.
/// </summary>
public sealed class NotFoundException : Exception
{
    public string EntityName { get; }
    public object EntityKey { get; }

    public NotFoundException(string entityName, object key)
        : base($"Entity \"{entityName}\" ({key}) was not found.")
    {
        EntityName = entityName;
        EntityKey = key;
    }
}
```

---

# Day 5: Domain Event Handlers

## 🧒 Explain Like I'm 5

Remember when we talked about domain events? 📣 They're like announcements:
- "Hey everyone! The pizza margin is too low!"

**Event handlers** are the people listening to those announcements:
- "I heard the margin is low! I'll create an alert!"
- "I heard the margin is low! I'll send a notification!"

## 🔧 Engineer Language

**Domain Event Handlers** react to domain events raised by aggregates. They:
- Implement side effects across aggregate boundaries
- Run in the same transaction (before `SaveChanges`)
- Are registered via MediatR's `INotificationHandler<T>`

> 📖 **Microsoft Docs**: *"When you use MediatR, each event handler must use an event type that is provided on the generic parameter of the `INotificationHandler` interface."*
>
> — [Domain events: Design and implementation](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation)

### MarginBelowThresholdEventHandler

Create `src/Nastart.Application/Recipe/EventHandlers/MarginBelowThresholdEventHandler.cs`:

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using Nastart.Application.Common.Interfaces;
using Nastart.Domain.Recipe.Aggregates;
using Nastart.Domain.Recipe.Events;

namespace Nastart.Application.Recipe.EventHandlers;

/// <summary>
/// Handles the MarginBelowThresholdEvent by creating an Alert.
/// This is the "side effect" that spans from Recipe to Alert aggregate.
/// </summary>
/// <remarks>
/// Domain event handlers run within the same transaction as the command
/// that raised the event. If any handler fails, the entire operation rolls back.
/// 
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation
/// </remarks>
public sealed class MarginBelowThresholdEventHandler 
    : INotificationHandler<MarginBelowThresholdEvent>
{
    private readonly IAlertRepository _alertRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<MarginBelowThresholdEventHandler> _logger;

    public MarginBelowThresholdEventHandler(
        IAlertRepository alertRepository,
        INotificationService notificationService,
        ILogger<MarginBelowThresholdEventHandler> logger)
    {
        _alertRepository = alertRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(
        MarginBelowThresholdEvent notification, 
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Low margin detected for recipe {RecipeName}: {Margin}% (threshold: {Threshold}%)",
            notification.RecipeName,
            notification.CurrentMarginPercentage,
            notification.ThresholdPercentage);

        // 1. Create an Alert aggregate
        var alert = Alert.CreateMarginAlert(
            notification.RecipeId,
            notification.RecipeName,
            notification.CurrentMarginPercentage,
            notification.ThresholdPercentage,
            notification.Severity
        );

        // 2. Add to repository (will be saved when UnitOfWork commits)
        await _alertRepository.AddAsync(alert, cancellationToken);

        // 3. Optionally send real-time notification
        await _notificationService.SendAlertAsync(
            $"⚠️ Low Margin Alert: {notification.RecipeName}",
            $"Margin is {notification.CurrentMarginPercentage:F1}%, below threshold of {notification.ThresholdPercentage}%",
            notification.Severity.ToString(),
            cancellationToken
        );

        _logger.LogInformation(
            "Alert created for low margin: AlertId = {AlertId}",
            alert.Id);
    }
}
```

### PriceSpikeDetectedEventHandler

Create `src/Nastart.Application/Finance/EventHandlers/PriceSpikeDetectedEventHandler.cs`:

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using Nastart.Application.Common.Interfaces;
using Nastart.Domain.Finance.Events;
using Nastart.Domain.Recipe.Aggregates;
using Nastart.Domain.Recipe.Events;

namespace Nastart.Application.Finance.EventHandlers;

/// <summary>
/// Handles the PriceSpikeDetectedEvent from Finance context.
/// Creates alerts and triggers recipe cost recalculation.
/// </summary>
public sealed class PriceSpikeDetectedEventHandler 
    : INotificationHandler<PriceSpikeDetectedEvent>
{
    private readonly IAlertRepository _alertRepository;
    private readonly IRecipeRepository _recipeRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PriceSpikeDetectedEventHandler> _logger;

    public PriceSpikeDetectedEventHandler(
        IAlertRepository alertRepository,
        IRecipeRepository recipeRepository,
        INotificationService notificationService,
        ILogger<PriceSpikeDetectedEventHandler> logger)
    {
        _alertRepository = alertRepository;
        _recipeRepository = recipeRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(
        PriceSpikeDetectedEvent notification, 
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Price spike detected for ingredient {IngredientId}: {OldPrice} -> {NewPrice} ({PercentageChange:F1}%)",
            notification.IngredientId,
            notification.OldPrice,
            notification.NewPrice,
            notification.PercentageChange);

        // 1. Create a price spike alert
        var alert = Alert.CreatePriceSpikeAlert(
            notification.IngredientId.Value,
            notification.IngredientName,
            notification.OldPrice,
            notification.NewPrice,
            notification.PercentageChange
        );

        await _alertRepository.AddAsync(alert, cancellationToken);

        // 2. Update recipe costs that use this ingredient
        // Note: This could also be done asynchronously via message queue
        var affectedRecipes = await _recipeRepository
            .GetByIngredientIdAsync(notification.IngredientId, cancellationToken);

        foreach (var recipe in affectedRecipes)
        {
            recipe.UpdateIngredientCost(notification.IngredientId, notification.NewPrice);
            _recipeRepository.Update(recipe);
            
            _logger.LogInformation(
                "Updated recipe {RecipeName} with new ingredient cost",
                recipe.Name);
        }

        // 3. Send notification
        await _notificationService.SendAlertAsync(
            $"🚨 Price Spike: {notification.IngredientName}",
            $"Price increased from ${notification.OldPrice:F2} to ${notification.NewPrice:F2} ({notification.PercentageChange:F1}% increase)",
            "High",
            cancellationToken
        );
    }
}
```

### PriceChangedEventHandler — Auto Recipe Recalculation

> 💡 **UX Enhancement**: When ingredient prices change, automatically recalculate all affected recipe costs. Users see up-to-date margins without manual refresh.

Create `src/Nastart.Application/Inventory/EventHandlers/PriceChangedEventHandler.cs`:

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using Nastart.Application.Common.Interfaces;
using Nastart.Domain.Inventory.Events;
using Nastart.Domain.Recipe.ValueObjects;

namespace Nastart.Application.Inventory.EventHandlers;

/// <summary>
/// Handles PriceChangedEvent to automatically recalculate recipe costs.
/// Ensures recipe margins always reflect current ingredient prices.
/// </summary>
/// <remarks>
/// UX Enhancement: Solves the "stale margin" problem where users see
/// outdated cost calculations. Now margins update automatically.
/// </remarks>
public sealed class PriceChangedEventHandler 
    : INotificationHandler<PriceChangedEvent>
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PriceChangedEventHandler> _logger;

    public PriceChangedEventHandler(
        IRecipeRepository recipeRepository,
        IUnitOfWork unitOfWork,
        INotificationService notificationService,
        ILogger<PriceChangedEventHandler> logger)
    {
        _recipeRepository = recipeRepository;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Handle(
        PriceChangedEvent notification, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Price changed for {IngredientName}: {OldPrice} → {NewPrice}",
            notification.IngredientName,
            notification.OldPrice.Amount,
            notification.NewPrice.Amount);

        // 1. Find all recipes using this ingredient
        var affectedRecipes = await _recipeRepository
            .GetByIngredientIdAsync(notification.IngredientId, cancellationToken);

        if (!affectedRecipes.Any())
        {
            _logger.LogDebug("No recipes affected by price change");
            return;
        }

        var recipesWithMarginImpact = new List<(string Name, decimal OldMargin, decimal NewMargin)>();

        // 2. Update each recipe's ingredient cost
        foreach (var recipe in affectedRecipes)
        {
            var oldMargin = recipe.CurrentMargin.Percentage;
            
            // Update the ingredient cost in the recipe
            recipe.UpdateIngredientCost(
                notification.IngredientId, 
                notification.NewPrice.Amount);
            
            var newMargin = recipe.CurrentMargin.Percentage;
            
            // Track significant margin changes
            if (Math.Abs(oldMargin - newMargin) > 1)
            {
                recipesWithMarginImpact.Add((recipe.Name, oldMargin, newMargin));
                
                // Mark active recipes for review if margin dropped significantly
                if (recipe.State == RecipeState.Active && newMargin < oldMargin - 5)
                {
                    recipe.MarkForReview($"Ingredient '{notification.IngredientName}' price changed");
                }
            }
            
            _recipeRepository.Update(recipe);
            
            _logger.LogInformation(
                "Recipe '{RecipeName}' cost updated: margin {OldMargin:F1}% → {NewMargin:F1}%",
                recipe.Name, oldMargin, newMargin);
        }

        // 3. Notify user of affected recipes
        if (recipesWithMarginImpact.Any())
        {
            var message = BuildRecipeImpactMessage(
                notification.IngredientName,
                notification.OldPrice.Amount,
                notification.NewPrice.Amount,
                recipesWithMarginImpact);
            
            await _notificationService.SendAlertAsync(
                $"📊 Recipe Margins Updated",
                message,
                recipesWithMarginImpact.Any(r => r.NewMargin < 15) ? "Warning" : "Info",
                cancellationToken);
        }

        _logger.LogInformation(
            "Updated {Count} recipes affected by price change to {Ingredient}",
            affectedRecipes.Count(),
            notification.IngredientName);
    }

    private static string BuildRecipeImpactMessage(
        string ingredientName,
        decimal oldPrice,
        decimal newPrice,
        List<(string Name, decimal OldMargin, decimal NewMargin)> recipes)
    {
        var changePercent = oldPrice > 0 
            ? ((newPrice - oldPrice) / oldPrice) * 100 
            : 0;
        var changeEmoji = changePercent > 0 ? "📈" : "📉";
        
        var lines = new List<string>
        {
            $"{changeEmoji} *{ingredientName}*: Rp {oldPrice:N0} → Rp {newPrice:N0} ({changePercent:+0.0;-0.0}%)",
            "",
            "*Affected Recipes:*"
        };

        foreach (var (name, oldMargin, newMargin) in recipes.OrderBy(r => r.NewMargin))
        {
            var marginEmoji = newMargin < 15 ? "⚠️" : newMargin < oldMargin ? "🔻" : "✅";
            lines.Add($"{marginEmoji} {name}: {oldMargin:F1}% → {newMargin:F1}%");
        }

        return string.Join("\n", lines);
    }
}
```

### RecipeCreatedEventHandler

Create `src/Nastart.Application/Recipe/EventHandlers/RecipeCreatedEventHandler.cs`:

```csharp
using MediatR;
using Microsoft.Extensions.Logging;
using Nastart.Domain.Recipe.Events;

namespace Nastart.Application.Recipe.EventHandlers;

/// <summary>
/// Handles the RecipeCreatedEvent.
/// Example of a simple event handler for logging/auditing.
/// </summary>
public sealed class RecipeCreatedEventHandler 
    : INotificationHandler<RecipeCreatedEvent>
{
    private readonly ILogger<RecipeCreatedEventHandler> _logger;

    public RecipeCreatedEventHandler(ILogger<RecipeCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(
        RecipeCreatedEvent notification, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Recipe created event processed: {RecipeId} - {RecipeName}",
            notification.RecipeId,
            notification.RecipeName);

        // Could also:
        // - Send welcome email
        // - Trigger analytics
        // - Update search index
        // - Sync with external systems

        return Task.CompletedTask;
    }
}
```

### Service Interfaces

Create `src/Nastart.Application/Common/Interfaces/INotificationService.cs`:

```csharp
namespace Nastart.Application.Common.Interfaces;

/// <summary>
/// Service for sending notifications (Telegram, WhatsApp, email, etc.).
/// Implemented in Infrastructure layer.
/// </summary>
public interface INotificationService
{
    Task SendAlertAsync(
        string title, 
        string message, 
        string severity, 
        CancellationToken cancellationToken = default);
    
    Task SendMessageAsync(
        string userId, 
        string message, 
        CancellationToken cancellationToken = default);
}
```

---

# Day 6: DTOs & Result Pattern

## 🧒 Explain Like I'm 5

When you order pizza 🍕, the restaurant doesn't give you the whole kitchen — just the pizza box with your order!

**DTOs** (Data Transfer Objects) are like pizza boxes — they contain just the data you need, nicely packaged.

**Result** is like a delivery receipt that says:
- ✅ "Your pizza was delivered!"
- ❌ "Sorry, we couldn't deliver. Here's why..."

## 🔧 Engineer Language

**DTOs/ViewModels** are data structures tailored for API responses. They:
- Decouple domain entities from API contracts
- Can combine data from multiple aggregates
- Don't contain business logic

**Result Pattern** provides explicit success/failure handling without exceptions:
- `Result.Success()` — Operation succeeded
- `Result.Failure(error)` — Operation failed with reason

> 📖 **Microsoft Docs**: *"The returned data (ViewModel) can be the result of joining data from multiple entities or tables in the database. Because you are creating queries independent of the domain model, the aggregates boundaries are ignored."*
>
> — [Implement reads/queries in a CQRS microservice](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/cqrs-microservice-reads)

### Result Pattern Implementation

Create `src/Nastart.Application/Common/Models/Result.cs`:

```csharp
namespace Nastart.Application.Common.Models;

/// <summary>
/// Represents the outcome of an operation that doesn't return a value.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }
    public IReadOnlyList<string> Errors { get; }

    protected Result(bool isSuccess, string? error, IEnumerable<string>? errors = null)
    {
        if (isSuccess && error is not null)
            throw new InvalidOperationException("Cannot have error on success.");
        
        if (!isSuccess && error is null && errors is null)
            throw new InvalidOperationException("Must have error on failure.");

        IsSuccess = isSuccess;
        Error = error;
        Errors = errors?.ToList() ?? (error is not null ? new List<string> { error } : new List<string>());
    }

    public static Result Success() => new(true, null);
    
    public static Result Failure(string error) => new(false, error);
    
    public static Result Failure(IEnumerable<string> errors) => new(false, null, errors);

    public static implicit operator Result(string error) => Failure(error);
}

/// <summary>
/// Represents the outcome of an operation that returns a value.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
public class Result<T> : Result
{
    private readonly T? _value;

    public T Value => IsSuccess 
        ? _value! 
        : throw new InvalidOperationException("Cannot access value on failed result.");

    protected Result(T value) : base(true, null)
    {
        _value = value;
    }

    protected Result(string error) : base(false, error)
    {
        _value = default;
    }

    protected Result(IEnumerable<string> errors) : base(false, null, errors)
    {
        _value = default;
    }

    public static Result<T> Success(T value) => new(value);
    
    public new static Result<T> Failure(string error) => new(error);
    
    public new static Result<T> Failure(IEnumerable<string> errors) => new(errors);

    public static implicit operator Result<T>(T value) => Success(value);
    
    public static implicit operator Result<T>(string error) => Failure(error);
}
```

### Result Extensions

Create `src/Nastart.Application/Common/Models/ResultExtensions.cs`:

```csharp
namespace Nastart.Application.Common.Models;

/// <summary>
/// Extension methods for Result pattern.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Maps a successful result to a new value.
    /// </summary>
    public static Result<TOut> Map<TIn, TOut>(
        this Result<TIn> result, 
        Func<TIn, TOut> mapper)
    {
        return result.IsSuccess 
            ? Result<TOut>.Success(mapper(result.Value)) 
            : Result<TOut>.Failure(result.Error!);
    }

    /// <summary>
    /// Binds a successful result to another result-returning operation.
    /// </summary>
    public static async Task<Result<TOut>> Bind<TIn, TOut>(
        this Result<TIn> result, 
        Func<TIn, Task<Result<TOut>>> binder)
    {
        return result.IsSuccess 
            ? await binder(result.Value) 
            : Result<TOut>.Failure(result.Error!);
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public static Result<T> OnSuccess<T>(
        this Result<T> result, 
        Action<T> action)
    {
        if (result.IsSuccess)
            action(result.Value);
        return result;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public static Result<T> OnFailure<T>(
        this Result<T> result, 
        Action<string> action)
    {
        if (result.IsFailure && result.Error is not null)
            action(result.Error);
        return result;
    }

    /// <summary>
    /// Matches the result to one of two functions.
    /// </summary>
    public static TOut Match<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> onSuccess,
        Func<string, TOut> onFailure)
    {
        return result.IsSuccess 
            ? onSuccess(result.Value) 
            : onFailure(result.Error ?? "Unknown error");
    }
}
```

### Using Result in Command Handlers

```csharp
public async Task<Result<Guid>> Handle(
    CreateRecipeCommand request, 
    CancellationToken cancellationToken)
{
    try
    {
        // ... create recipe
        return Result<Guid>.Success(recipe.Id.Value);
    }
    catch (ArgumentException ex)
    {
        return Result<Guid>.Failure($"Validation error: {ex.Message}");
    }
}
```

### Using Result in Controllers (Preview)

```csharp
[HttpPost]
public async Task<IActionResult> CreateRecipe(
    [FromBody] CreateRecipeCommand command,
    CancellationToken cancellationToken)
{
    var result = await _mediator.Send(command, cancellationToken);

    return result.Match(
        onSuccess: id => Ok(new { RecipeId = id }),
        onFailure: error => BadRequest(new { Error = error })
    );
}
```

### More DTOs

Create `src/Nastart.Application/Recipe/Queries/GetRecipeById/RecipeSummaryDto.cs`:

```csharp
namespace Nastart.Application.Recipe.Queries.GetRecipeById;

/// <summary>
/// Lightweight DTO for recipe listings.
/// Less data than RecipeDetailsDto.
/// </summary>
public sealed record RecipeSummaryDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required decimal SellingPrice { get; init; }
    public required decimal MarginPercentage { get; init; }
    public required string MarginStatus { get; init; }
    public required bool IsActive { get; init; }
    public required int IngredientCount { get; init; }
}
```

---

# Day 7: Integration Testing Application Layer

## 🧒 Explain Like I'm 5

After building with LEGOs 🧱, you check if everything fits together properly. You don't just look at each piece — you check the whole creation!

**Integration tests** make sure all the parts work together:
- "Can I create a recipe and then find it?"
- "Does the alert get created when the margin is too low?"

## 🔧 Engineer Language

**Integration tests** for the Application layer verify that:
- Commands create/modify aggregates correctly
- Queries return expected data
- Domain events trigger handlers
- Validation works end-to-end

### Testing Setup

Update `tests/Nastart.Application.Tests/Nastart.Application.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="FluentAssertions" Version="7.*" />
    <PackageReference Include="NSubstitute" Version="5.*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Nastart.Application\Nastart.Application.csproj" />
  </ItemGroup>

</Project>
```

### Testing CreateRecipeCommandHandler

Create `tests/Nastart.Application.Tests/Recipe/Commands/CreateRecipeCommandHandlerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Nastart.Application.Common.Interfaces;
using Nastart.Application.Recipe.Commands.CreateRecipe;
using Nastart.Domain.Recipe.Aggregates;
using Nastart.Domain.Recipe.ValueObjects;

namespace Nastart.Application.Tests.Recipe.Commands;

public class CreateRecipeCommandHandlerTests
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateRecipeCommandHandler> _logger;
    private readonly CreateRecipeCommandHandler _handler;

    public CreateRecipeCommandHandlerTests()
    {
        _recipeRepository = Substitute.For<IRecipeRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _logger = Substitute.For<ILogger<CreateRecipeCommandHandler>>();
        
        _handler = new CreateRecipeCommandHandler(
            _recipeRepository,
            _unitOfWork,
            _logger
        );
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateRecipeAndReturnId()
    {
        // Arrange
        var command = new CreateRecipeCommand
        {
            Name = "Margherita Pizza",
            Category = "Main Course",
            SellingPrice = 15.00m,
            Description = "Classic Italian pizza"
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);

        await _recipeRepository.Received(1).AddAsync(
            Arg.Is<Domain.Recipe.Aggregates.Recipe>(r => 
                r.Name == "Margherita Pizza" &&
                r.SellingPrice == 15.00m),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInitialIngredients_ShouldAddIngredientsToRecipe()
    {
        // Arrange
        var command = new CreateRecipeCommand
        {
            Name = "Pizza",
            Category = "Main",
            SellingPrice = 15.00m,
            InitialIngredients = new List<RecipeIngredientDto>
            {
                new()
                {
                    IngredientId = Guid.NewGuid(),
                    IngredientName = "Flour",
                    Quantity = 0.5m,
                    Unit = "kg",
                    UnitCost = 2.00m
                }
            }
        };

        Domain.Recipe.Aggregates.Recipe? capturedRecipe = null;
        await _recipeRepository.AddAsync(
            Arg.Do<Domain.Recipe.Aggregates.Recipe>(r => capturedRecipe = r),
            Arg.Any<CancellationToken>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedRecipe.Should().NotBeNull();
        capturedRecipe!.Items.Should().HaveCount(1);
        capturedRecipe.Items.First().IngredientName.Should().Be("Flour");
    }

    [Fact]
    public async Task Handle_WithEmptyName_ShouldReturnFailure()
    {
        // Arrange
        var command = new CreateRecipeCommand
        {
            Name = "",  // Invalid
            Category = "Main",
            SellingPrice = 15.00m
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("name");

        await _recipeRepository.DidNotReceive()
            .AddAsync(Arg.Any<Domain.Recipe.Aggregates.Recipe>(), Arg.Any<CancellationToken>());
    }
}
```

### Testing CreateRecipeCommandValidator

Create `tests/Nastart.Application.Tests/Recipe/Commands/CreateRecipeCommandValidatorTests.cs`:

```csharp
using FluentAssertions;
using Nastart.Application.Recipe.Commands.CreateRecipe;

namespace Nastart.Application.Tests.Recipe.Commands;

public class CreateRecipeCommandValidatorTests
{
    private readonly CreateRecipeCommandValidator _validator = new();

    [Fact]
    public async Task Validate_WithValidCommand_ShouldPass()
    {
        // Arrange
        var command = new CreateRecipeCommand
        {
            Name = "Test Recipe",
            Category = "Main",
            SellingPrice = 10.00m
        };

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Validate_WithEmptyName_ShouldFail(string? name)
    {
        // Arrange
        var command = new CreateRecipeCommand
        {
            Name = name!,
            Category = "Main",
            SellingPrice = 10.00m
        };

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Validate_WithInvalidPrice_ShouldFail(decimal price)
    {
        // Arrange
        var command = new CreateRecipeCommand
        {
            Name = "Test",
            Category = "Main",
            SellingPrice = price
        };

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SellingPrice");
    }

    [Fact]
    public async Task Validate_WithNameTooLong_ShouldFail()
    {
        // Arrange
        var command = new CreateRecipeCommand
        {
            Name = new string('A', 201),  // Max is 200
            Category = "Main",
            SellingPrice = 10.00m
        };

        // Act
        var result = await _validator.ValidateAsync(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => 
            e.PropertyName == "Name" && 
            e.ErrorMessage.Contains("200"));
    }
}
```

### Testing Domain Event Handler

Create `tests/Nastart.Application.Tests/Recipe/EventHandlers/MarginBelowThresholdEventHandlerTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Nastart.Application.Common.Interfaces;
using Nastart.Application.Recipe.EventHandlers;
using Nastart.Domain.Recipe.Aggregates;
using Nastart.Domain.Recipe.Events;
using Nastart.Domain.Recipe.ValueObjects;

namespace Nastart.Application.Tests.Recipe.EventHandlers;

public class MarginBelowThresholdEventHandlerTests
{
    private readonly IAlertRepository _alertRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<MarginBelowThresholdEventHandler> _logger;
    private readonly MarginBelowThresholdEventHandler _handler;

    public MarginBelowThresholdEventHandlerTests()
    {
        _alertRepository = Substitute.For<IAlertRepository>();
        _notificationService = Substitute.For<INotificationService>();
        _logger = Substitute.For<ILogger<MarginBelowThresholdEventHandler>>();
        
        _handler = new MarginBelowThresholdEventHandler(
            _alertRepository,
            _notificationService,
            _logger
        );
    }

    [Fact]
    public async Task Handle_ShouldCreateAlertAndSendNotification()
    {
        // Arrange
        var domainEvent = new MarginBelowThresholdEvent(
            RecipeId: RecipeId.New(),
            RecipeName: "Low Margin Pizza",
            CurrentMarginPercentage: 8m,
            ThresholdPercentage: 15m
        );

        Alert? capturedAlert = null;
        await _alertRepository.AddAsync(
            Arg.Do<Alert>(a => capturedAlert = a),
            Arg.Any<CancellationToken>());

        // Act
        await _handler.Handle(domainEvent, CancellationToken.None);

        // Assert
        await _alertRepository.Received(1).AddAsync(
            Arg.Any<Alert>(),
            Arg.Any<CancellationToken>());

        capturedAlert.Should().NotBeNull();
        capturedAlert!.Title.Should().Contain("Low Margin Pizza");
        capturedAlert.Type.Should().Be(AlertType.LowMargin);

        await _notificationService.Received(1).SendAlertAsync(
            Arg.Is<string>(s => s.Contains("Low Margin")),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
```

### Running the Tests

```powershell
cd c:\Users\AU1833\Documents\personal\nastart\backend

# Run all application layer tests
dotnet test tests/Nastart.Application.Tests

# Run with coverage
dotnet test tests/Nastart.Application.Tests --collect:"XPlat Code Coverage"

# Run specific test
dotnet test tests/Nastart.Application.Tests --filter "FullyQualifiedName~CreateRecipeCommand"
```

---

# Resources

## Microsoft Official Documentation (Verified)

| Topic | Link |
|-------|------|
| Apply simplified CQRS and DDD patterns | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/apply-simplified-microservice-cqrs-ddd-patterns |
| Implement the microservice application layer | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api |
| Domain events: Design and implementation | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation |
| Implement reads/queries in CQRS | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/cqrs-microservice-reads |
| CQRS Pattern | https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs |

## Folder Structure After Week 5

```
src/Nastart.Application/
├── Common/
│   ├── Behaviors/
│   │   ├── LoggingBehavior.cs         ✅
│   │   ├── ValidationBehavior.cs      ✅
│   │   └── PerformanceBehavior.cs     ✅
│   ├── Exceptions/
│   │   ├── ValidationException.cs     ✅
│   │   └── NotFoundException.cs       ✅
│   ├── Interfaces/
│   │   ├── IRecipeRepository.cs       ✅
│   │   ├── IRecipeQueries.cs          ✅
│   │   ├── IUnitOfWork.cs             ✅
│   │   ├── IAlertRepository.cs        ✅
│   │   └── INotificationService.cs    ✅
│   ├── Models/
│   │   ├── Result.cs                  ✅
│   │   └── ResultExtensions.cs        ✅
│   └── Queries/
│       └── GetDashboardSummary/       ✅
│
├── Recipe/
│   ├── Commands/
│   │   ├── CreateRecipe/
│   │   │   ├── CreateRecipeCommand.cs          ✅
│   │   │   ├── CreateRecipeCommandHandler.cs   ✅
│   │   │   └── CreateRecipeCommandValidator.cs ✅
│   │   └── AddIngredientToRecipe/
│   │       ├── AddIngredientToRecipeCommand.cs          ✅
│   │       ├── AddIngredientToRecipeCommandHandler.cs   ✅
│   │       └── AddIngredientToRecipeCommandValidator.cs ✅
│   ├── Queries/
│   │   ├── GetRecipeById/
│   │   │   ├── GetRecipeByIdQuery.cs           ✅
│   │   │   ├── GetRecipeByIdQueryHandler.cs    ✅
│   │   │   ├── RecipeDetailsDto.cs             ✅
│   │   │   └── RecipeSummaryDto.cs             ✅
│   │   └── GetLowMarginRecipes/
│   │       └── ...                              ✅
│   └── EventHandlers/
│       ├── MarginBelowThresholdEventHandler.cs ✅
│       └── RecipeCreatedEventHandler.cs        ✅
│
├── Finance/
│   ├── Commands/
│   │   └── RecordPurchase/
│   │       └── ...                              ✅
│   └── EventHandlers/
│       └── PriceSpikeDetectedEventHandler.cs   ✅
│
└── DependencyInjection.cs                       ✅

tests/Nastart.Application.Tests/
├── Recipe/
│   ├── Commands/
│   │   ├── CreateRecipeCommandHandlerTests.cs     ✅
│   │   └── CreateRecipeCommandValidatorTests.cs   ✅
│   └── EventHandlers/
│       └── MarginBelowThresholdEventHandlerTests.cs ✅
```

## Week 5 Checklist

- [x] Install MediatR, FluentValidation packages
- [x] Create `DependencyInjection.cs` for service registration
- [x] Create Commands: `CreateRecipeCommand`, `AddIngredientToRecipeCommand`, `RecordPurchaseCommand`
- [x] Create Command Handlers with repository pattern
- [x] Create Queries: `GetRecipeByIdQuery`, `GetLowMarginRecipesQuery`, `GetDashboardSummaryQuery`
- [x] Create Query Handlers with DTOs
- [x] Create Pipeline Behaviors: `LoggingBehavior`, `ValidationBehavior`, `PerformanceBehavior`
- [x] Create FluentValidation validators for commands
- [x] Create Domain Event Handlers: `MarginBelowThresholdEventHandler`, `PriceSpikeDetectedEventHandler`
- [x] Create Result pattern with extensions
- [x] Create custom exceptions: `ValidationException`, `NotFoundException`
- [x] Write unit tests for handlers and validators

---

## Next Week Preview: Week 6 — Infrastructure Layer (Persistence)

In Week 6, we'll build the Infrastructure Layer:
- **DbContext**: `NastartDbContext` with EF Core
- **Entity Configurations**: Map aggregates, value objects, owned entities
- **Repositories**: Implement `IRecipeRepository`, `IPurchaseRepository`
- **Unit of Work**: Transaction management and domain event dispatching
- **Migrations**: Database schema creation

---

*Last Updated: Week 5 Lesson*
*Stack: .NET 10, C# 13, MediatR, FluentValidation, CQRS*
