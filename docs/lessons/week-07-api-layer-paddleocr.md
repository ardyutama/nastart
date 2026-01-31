# Week 7: API Layer + PaddleOCR Integration 🌐

> **Goal**: Build the API Layer — Controllers with MediatR, Minimal APIs, Global Exception Handling, OpenAPI documentation, and integrate PaddleOCR Python microservice for receipt scanning.

---

## Table of Contents
1. [Day 1: API Architecture & Project Setup](#day-1-api-architecture--project-setup)
2. [Day 2: Controllers with MediatR](#day-2-controllers-with-mediatr)
3. [Day 3: Minimal APIs & Route Groups](#day-3-minimal-apis--route-groups)
4. [Day 4: Global Exception Handling & ProblemDetails](#day-4-global-exception-handling--problemdetails)
5. [Day 5: PaddleOCR Python Microservice](#day-5-paddleocr-python-microservice)
6. [Day 6: HttpClient & OCR Service Integration](#day-6-httpclient--ocr-service-integration)
7. [Day 7: OpenAPI Documentation & API Versioning](#day-7-openapi-documentation--api-versioning)
8. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1: API Architecture & Project Setup

## 🧒 Explain Like I'm 5

The **API** is like a waiter in a restaurant 🍽️:
- You (the app) tell the waiter what you want
- The waiter goes to the kitchen (domain/application layer)
- The waiter brings back your food (response)

The API doesn't cook — it just takes orders and delivers!

## 🔧 Engineer Language

The **API Layer** is the entry point to your application:
- Receives HTTP requests
- Dispatches commands/queries via MediatR
- Returns HTTP responses
- Handles cross-cutting concerns (auth, validation, logging)

> 📖 **Microsoft Docs**: *"REST APIs should use attribute routing to model the app's functionality as a set of resources where operations are represented by HTTP verbs."*
>
> — [Routing to controller actions](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/routing)

### API Layer Responsibilities

| Layer | Responsibility |
|-------|---------------|
| **API** | HTTP handling, routing, serialization |
| **Application** | Business use cases, CQRS |
| **Domain** | Business rules, entities |
| **Infrastructure** | Persistence, external services |

### API Project Structure

```
src/Nastart.Api/
├── Controllers/
│   ├── IngredientsController.cs
│   ├── RecipesController.cs
│   ├── PurchasesController.cs
│   └── AlertsController.cs
│
├── Endpoints/                    # Minimal API alternative
│   ├── IngredientEndpoints.cs
│   ├── RecipeEndpoints.cs
│   └── ReceiptEndpoints.cs
│
├── Middleware/
│   └── GlobalExceptionHandler.cs
│
├── Contracts/                    # Request/Response DTOs
│   ├── Requests/
│   │   ├── CreateIngredientRequest.cs
│   │   ├── CreateRecipeRequest.cs
│   │   └── ScanReceiptRequest.cs
│   └── Responses/
│       ├── IngredientResponse.cs
│       ├── RecipeResponse.cs
│       └── ReceiptScanResponse.cs
│
├── Extensions/
│   └── ServiceCollectionExtensions.cs
│
├── Properties/
│   └── launchSettings.json
│
├── appsettings.json
├── appsettings.Development.json
├── Program.cs
└── Nastart.Api.csproj
```

### Install Required Packages

```powershell
cd c:\Users\AU1833\Documents\personal\nastart\backend

# API Layer packages
dotnet add src/Nastart.Api/Nastart.Api.csproj package Swashbuckle.AspNetCore
dotnet add src/Nastart.Api/Nastart.Api.csproj package Swashbuckle.AspNetCore.Annotations

# Add project references
dotnet add src/Nastart.Api/Nastart.Api.csproj reference src/Nastart.Application/Nastart.Application.csproj
dotnet add src/Nastart.Api/Nastart.Api.csproj reference src/Nastart.Infrastructure/Nastart.Infrastructure.csproj
```

### Update Program.cs

Update `src/Nastart.Api/Program.cs`:

```csharp
using Nastart.Api.Middleware;
using Nastart.Application;
using Nastart.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add API services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger/OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Version = "v1",
        Title = "Nastart API",
        Description = "Smart inventory & finance tracking for small F&B businesses",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Nastart Support",
            Email = "support@nastart.app"
        }
    });
});

// Add ProblemDetails for consistent error responses
builder.Services.AddProblemDetails();

// Add exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// Configure pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Nastart API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

---

# Day 2: Controllers with MediatR

## 🧒 Explain Like I'm 5

A **controller** is like a receptionist 📞:
- Answers the phone (HTTP request)
- Writes down your message (creates a command)
- Gives it to the right person (MediatR sends to handler)
- Calls you back with the answer (HTTP response)

## 🔧 Engineer Language

**Controllers** receive HTTP requests and delegate to MediatR:
- No business logic in controllers
- Controllers are thin — just routing + serialization
- Use `[ApiController]` attribute for automatic model binding

> 📖 **Microsoft Docs**: *"The MyMicroserviceController constructor uses DI to inject IMediator and query services, simplifying the controller's dependencies by using the Mediator pattern."*
>
> — [Implement the command process pipeline with MediatR](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api)

### IngredientsController

Create `src/Nastart.Api/Controllers/IngredientsController.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nastart.Api.Contracts.Requests;
using Nastart.Api.Contracts.Responses;
using Nastart.Application.Inventory.Commands.CreateIngredient;
using Nastart.Application.Inventory.Commands.UpdateIngredient;
using Nastart.Application.Inventory.Queries.GetIngredientById;
using Nastart.Application.Inventory.Queries.GetIngredients;
using Nastart.Application.Inventory.Queries.GetLowStockIngredients;
using Swashbuckle.AspNetCore.Annotations;

namespace Nastart.Api.Controllers;

/// <summary>
/// Controller for managing ingredients.
/// </summary>
/// <remarks>
/// Following Microsoft's eShopOnContainers pattern:
/// - Controllers are thin — only routing + serialization
/// - All business logic delegated to MediatR handlers
/// - Use [ApiController] for automatic model validation
/// 
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api
/// </remarks>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class IngredientsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<IngredientsController> _logger;

    public IngredientsController(IMediator mediator, ILogger<IngredientsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all ingredients.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "Get all ingredients", Description = "Returns a list of all ingredients")]
    [ProducesResponseType(typeof(IEnumerable<IngredientResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting all ingredients");
        
        var query = new GetIngredientsQuery();
        var result = await _mediator.Send(query, cancellationToken);
        
        return Ok(result);
    }

    /// <summary>
    /// Gets an ingredient by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Summary = "Get ingredient by ID")]
    [ProducesResponseType(typeof(IngredientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting ingredient {IngredientId}", id);
        
        var query = new GetIngredientByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);
        
        if (result is null)
            return NotFound();
        
        return Ok(result);
    }

    /// <summary>
    /// Gets ingredients with low stock.
    /// </summary>
    [HttpGet("low-stock")]
    [SwaggerOperation(Summary = "Get low stock ingredients", 
        Description = "Returns ingredients below their minimum stock threshold")]
    [ProducesResponseType(typeof(IEnumerable<IngredientResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowStock(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting low stock ingredients");
        
        var query = new GetLowStockIngredientsQuery();
        var result = await _mediator.Send(query, cancellationToken);
        
        return Ok(result);
    }

    /// <summary>
    /// Creates a new ingredient.
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Create a new ingredient")]
    [ProducesResponseType(typeof(IngredientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateIngredientRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating ingredient {Name}", request.Name);
        
        var command = new CreateIngredientCommand(
            Name: request.Name,
            Unit: request.Unit,
            CurrentPrice: request.CurrentPrice,
            CurrentStock: request.CurrentStock,
            MinimumStock: request.MinimumStock,
            CategoryId: request.CategoryId
        );
        
        var result = await _mediator.Send(command, cancellationToken);
        
        if (!result.IsSuccess)
            return BadRequest(result.Error);
        
        return CreatedAtAction(
            nameof(GetById), 
            new { id = result.Value.Id }, 
            result.Value);
    }

    /// <summary>
    /// Updates an ingredient.
    /// </summary>
    [HttpPut("{id:guid}")]
    [SwaggerOperation(Summary = "Update an ingredient")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateIngredientRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating ingredient {IngredientId}", id);
        
        var command = new UpdateIngredientCommand(
            Id: id,
            Name: request.Name,
            CurrentPrice: request.CurrentPrice,
            MinimumStock: request.MinimumStock
        );
        
        var result = await _mediator.Send(command, cancellationToken);
        
        if (!result.IsSuccess)
        {
            if (result.Error?.Code == "NotFound")
                return NotFound();
            
            return BadRequest(result.Error);
        }
        
        return NoContent();
    }
}
```

### RecipesController

Create `src/Nastart.Api/Controllers/RecipesController.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Nastart.Api.Contracts.Requests;
using Nastart.Api.Contracts.Responses;
using Nastart.Application.Recipe.Commands.CreateRecipe;
using Nastart.Application.Recipe.Commands.AddIngredientToRecipe;
using Nastart.Application.Recipe.Commands.RecalculateRecipeCost;
using Nastart.Application.Recipe.Queries.GetRecipeById;
using Nastart.Application.Recipe.Queries.GetRecipes;
using Nastart.Application.Recipe.Queries.GetRecipeCost;
using Swashbuckle.AspNetCore.Annotations;

namespace Nastart.Api.Controllers;

/// <summary>
/// Controller for managing recipes.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class RecipesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<RecipesController> _logger;

    public RecipesController(IMediator mediator, ILogger<RecipesController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all active recipes.
    /// </summary>
    [HttpGet]
    [SwaggerOperation(Summary = "Get all recipes")]
    [ProducesResponseType(typeof(IEnumerable<RecipeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var query = new GetRecipesQuery();
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets a recipe by ID with full cost breakdown.
    /// </summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Summary = "Get recipe by ID", 
        Description = "Returns recipe with ingredients and cost breakdown")]
    [ProducesResponseType(typeof(RecipeDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetRecipeByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);
        
        if (result is null)
            return NotFound();
        
        return Ok(result);
    }

    /// <summary>
    /// Gets the cost breakdown for a recipe.
    /// </summary>
    [HttpGet("{id:guid}/cost")]
    [SwaggerOperation(Summary = "Get recipe cost breakdown")]
    [ProducesResponseType(typeof(RecipeCostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCost(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetRecipeCostQuery(id);
        var result = await _mediator.Send(query, cancellationToken);
        
        if (result is null)
            return NotFound();
        
        return Ok(result);
    }

    /// <summary>
    /// Creates a new recipe.
    /// </summary>
    [HttpPost]
    [SwaggerOperation(Summary = "Create a new recipe")]
    [ProducesResponseType(typeof(RecipeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRecipeRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating recipe {Name}", request.Name);
        
        var command = new CreateRecipeCommand(
            Name: request.Name,
            SellingPrice: request.SellingPrice,
            Category: request.Category,
            Description: request.Description
        );
        
        var result = await _mediator.Send(command, cancellationToken);
        
        if (!result.IsSuccess)
            return BadRequest(result.Error);
        
        return CreatedAtAction(
            nameof(GetById), 
            new { id = result.Value.Id }, 
            result.Value);
    }

    /// <summary>
    /// Adds an ingredient to a recipe.
    /// </summary>
    [HttpPost("{id:guid}/ingredients")]
    [SwaggerOperation(Summary = "Add ingredient to recipe")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddIngredient(
        Guid id,
        [FromBody] AddIngredientToRecipeRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddIngredientToRecipeCommand(
            RecipeId: id,
            IngredientId: request.IngredientId,
            Quantity: request.Quantity,
            Unit: request.Unit
        );
        
        var result = await _mediator.Send(command, cancellationToken);
        
        if (!result.IsSuccess)
        {
            if (result.Error?.Code == "NotFound")
                return NotFound();
            
            return BadRequest(result.Error);
        }
        
        return NoContent();
    }

    /// <summary>
    /// Recalculates recipe cost based on current ingredient prices.
    /// </summary>
    [HttpPost("{id:guid}/recalculate")]
    [SwaggerOperation(Summary = "Recalculate recipe cost", 
        Description = "Updates recipe cost based on current ingredient prices")]
    [ProducesResponseType(typeof(RecipeCostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecalculateCost(
        Guid id, 
        CancellationToken cancellationToken)
    {
        var command = new RecalculateRecipeCostCommand(id);
        var result = await _mediator.Send(command, cancellationToken);
        
        if (!result.IsSuccess)
            return NotFound();
        
        return Ok(result.Value);
    }
}
```

### Request/Response DTOs

Create `src/Nastart.Api/Contracts/Requests/CreateIngredientRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Nastart.Api.Contracts.Requests;

/// <summary>
/// Request to create a new ingredient.
/// </summary>
public record CreateIngredientRequest(
    [Required, StringLength(200)] string Name,
    [Required, StringLength(20)] string Unit,
    [Required, Range(0, double.MaxValue)] decimal CurrentPrice,
    [Range(0, double.MaxValue)] decimal CurrentStock = 0,
    [Range(0, double.MaxValue)] decimal MinimumStock = 0,
    Guid? CategoryId = null
);

/// <summary>
/// Request to update an ingredient.
/// </summary>
public record UpdateIngredientRequest(
    [StringLength(200)] string? Name,
    [Range(0, double.MaxValue)] decimal? CurrentPrice,
    [Range(0, double.MaxValue)] decimal? MinimumStock
);
```

Create `src/Nastart.Api/Contracts/Requests/CreateRecipeRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace Nastart.Api.Contracts.Requests;

/// <summary>
/// Request to create a new recipe.
/// </summary>
public record CreateRecipeRequest(
    [Required, StringLength(200)] string Name,
    [Required, Range(0, double.MaxValue)] decimal SellingPrice,
    [Required, StringLength(100)] string Category,
    [StringLength(2000)] string? Description = null
);

/// <summary>
/// Request to add an ingredient to a recipe.
/// </summary>
public record AddIngredientToRecipeRequest(
    [Required] Guid IngredientId,
    [Required, Range(0.0001, double.MaxValue)] decimal Quantity,
    [Required, StringLength(20)] string Unit
);
```

Create `src/Nastart.Api/Contracts/Responses/IngredientResponse.cs`:

```csharp
namespace Nastart.Api.Contracts.Responses;

/// <summary>
/// Response representing an ingredient.
/// </summary>
public record IngredientResponse(
    Guid Id,
    string Name,
    string Unit,
    decimal CurrentPrice,
    string Currency,
    decimal CurrentStock,
    decimal MinimumStock,
    bool IsLowStock,
    Guid? CategoryId,
    string? CategoryName
);
```

Create `src/Nastart.Api/Contracts/Responses/RecipeResponse.cs`:

```csharp
namespace Nastart.Api.Contracts.Responses;

/// <summary>
/// Response representing a recipe summary.
/// </summary>
public record RecipeResponse(
    Guid Id,
    string Name,
    string Category,
    decimal SellingPrice,
    decimal TotalCost,
    decimal MarginPercentage,
    bool IsActive
);

/// <summary>
/// Response representing a recipe with full details.
/// </summary>
public record RecipeDetailResponse(
    Guid Id,
    string Name,
    string Category,
    string? Description,
    decimal SellingPrice,
    decimal TotalCost,
    decimal MarginPercentage,
    decimal MarginAmount,
    bool IsActive,
    IReadOnlyList<RecipeItemResponse> Items
);

/// <summary>
/// Response representing a recipe ingredient item.
/// </summary>
public record RecipeItemResponse(
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    string Unit,
    decimal UnitCost,
    decimal TotalCost
);

/// <summary>
/// Response representing recipe cost breakdown.
/// </summary>
public record RecipeCostResponse(
    Guid RecipeId,
    string RecipeName,
    decimal SellingPrice,
    decimal TotalCost,
    decimal MarginPercentage,
    decimal MarginAmount,
    IReadOnlyList<RecipeItemResponse> CostBreakdown
);
```

---

# Day 3: Minimal APIs & Route Groups

## 🧒 Explain Like I'm 5

**Minimal APIs** are like sending a text message 📱 instead of writing a letter:
- Shorter and faster
- Same information, less paper
- Great for simple messages!

## 🔧 Engineer Language

**Minimal APIs** provide a lightweight alternative to controllers:
- Less ceremony, faster to write
- Great for simple CRUD endpoints
- Use `MapGroup` to organize routes
- Use `TypedResults` for automatic OpenAPI metadata

> 📖 **Microsoft Docs**: *"The MapGroup extension method helps organize groups of endpoints with a common prefix. It reduces repetitive code and allows for customizing entire groups."*
>
> — [Minimal APIs quick reference](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)

### Category Endpoints (Minimal API)

Create `src/Nastart.Api/Endpoints/CategoryEndpoints.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Nastart.Application.Inventory.Queries.GetCategories;

namespace Nastart.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for categories.
/// </summary>
/// <remarks>
/// Demonstrates .NET 10 Minimal API patterns:
/// - Route groups for organization
/// - TypedResults for automatic OpenAPI metadata
/// - Method handlers outside Program.cs
/// 
/// See: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis
/// </remarks>
public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/categories")
            .WithTags("Categories")
            .WithOpenApi();

        group.MapGet("/", GetAllCategories)
            .WithName("GetCategories")
            .WithSummary("Get all categories");

        group.MapGet("/{id:guid}", GetCategoryById)
            .WithName("GetCategoryById")
            .WithSummary("Get category by ID");
    }

    /// <summary>
    /// Gets all categories.
    /// </summary>
    private static async Task<Ok<IEnumerable<CategoryDto>>> GetAllCategories(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetCategoriesQuery();
        var result = await mediator.Send(query, cancellationToken);
        return TypedResults.Ok(result);
    }

    /// <summary>
    /// Gets a category by ID.
    /// </summary>
    private static async Task<Results<Ok<CategoryDto>, NotFound>> GetCategoryById(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetCategoryByIdQuery(id);
        var result = await mediator.Send(query, cancellationToken);
        
        return result is not null 
            ? TypedResults.Ok(result) 
            : TypedResults.NotFound();
    }
}

// Simple DTO for categories
public record CategoryDto(Guid Id, string Name, string? Description);
```

### Receipt Scanning Endpoints

Create `src/Nastart.Api/Endpoints/ReceiptEndpoints.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Nastart.Api.Contracts.Responses;
using Nastart.Application.Finance.Commands.ScanReceipt;

namespace Nastart.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for receipt scanning.
/// </summary>
public static class ReceiptEndpoints
{
    public static void MapReceiptEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/receipts")
            .WithTags("Receipts")
            .WithOpenApi();

        group.MapPost("/scan", ScanReceipt)
            .WithName("ScanReceipt")
            .WithSummary("Scan a receipt image using OCR")
            .DisableAntiforgery(); // Required for file uploads
    }

    /// <summary>
    /// Scans a receipt image and extracts items.
    /// </summary>
    private static async Task<Results<Ok<ReceiptScanResponse>, BadRequest<string>>> ScanReceipt(
        IFormFile file,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return TypedResults.BadRequest("No file uploaded");
        }

        // Validate file type
        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType))
        {
            return TypedResults.BadRequest("Invalid file type. Allowed: JPEG, PNG, WebP");
        }

        logger.LogInformation("Scanning receipt: {FileName}, Size: {Size} bytes", 
            file.FileName, file.Length);

        // Read file into byte array
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream, cancellationToken);
        var imageBytes = memoryStream.ToArray();

        var command = new ScanReceiptCommand(
            ImageBytes: imageBytes,
            FileName: file.FileName
        );

        var result = await mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return TypedResults.BadRequest(result.Error?.Message ?? "Scan failed");
        }

        return TypedResults.Ok(result.Value);
    }
}
```

### Register Minimal API Endpoints in Program.cs

Update `src/Nastart.Api/Program.cs`:

```csharp
using Nastart.Api.Endpoints;
using Nastart.Api.Middleware;
using Nastart.Application;
using Nastart.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ... existing service registration ...

var app = builder.Build();

// ... existing middleware ...

app.UseHttpsRedirection();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map Minimal API endpoints
app.MapCategoryEndpoints();
app.MapReceiptEndpoints();

app.Run();
```

### TypedResults vs Results

| Aspect | `Results` | `TypedResults` |
|--------|-----------|----------------|
| Return Type | `IResult` | Concrete type (e.g., `Ok<T>`) |
| OpenAPI Metadata | Requires `.Produces<T>()` | Automatic |
| Testability | Cast required | Type-safe assertions |
| Multiple return types | Just use | Requires `Results<T1, T2>` |

```csharp
// Results — needs .Produces() for OpenAPI
app.MapGet("/items/{id}", async (int id) => 
    await db.FindAsync(id) is Item item 
        ? Results.Ok(item) 
        : Results.NotFound())
    .Produces<Item>()
    .Produces(404);

// TypedResults — automatic OpenAPI metadata
app.MapGet("/items/{id}", async Task<Results<Ok<Item>, NotFound>> (int id) => 
    await db.FindAsync(id) is Item item 
        ? TypedResults.Ok(item) 
        : TypedResults.NotFound());
```

---

# Day 4: Global Exception Handling & ProblemDetails

## 🧒 Explain Like I'm 5

When something goes wrong 💥, you don't want to show a scary error! Instead, you give a nice, friendly message that explains what happened.

**ProblemDetails** is like saying "Sorry, we couldn't find your toy" instead of screaming "NULL REFERENCE EXCEPTION AT LINE 47!!!"

## 🔧 Engineer Language

**ProblemDetails** is the RFC 7807 standard for error responses:
- Consistent error format across all endpoints
- Machine-readable error details
- Human-readable titles and descriptions

> 📖 **Microsoft Docs**: *"The ProblemDetails middleware writes problem details responses for HTTP client and server error responses."*
>
> — [Handle errors in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling)

### Global Exception Handler

Create `src/Nastart.Api/Middleware/GlobalExceptionHandler.cs`:

```csharp
using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Nastart.Domain.Common.Exceptions;

namespace Nastart.Api.Middleware;

/// <summary>
/// Global exception handler for consistent error responses.
/// </summary>
/// <remarks>
/// Following Microsoft's ProblemDetails pattern:
/// - RFC 7807 compliant error responses
/// - Maps domain exceptions to HTTP status codes
/// - Includes correlation ID for debugging
/// 
/// See: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling
/// </remarks>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

        var problemDetails = exception switch
        {
            ValidationException validationException => CreateValidationProblem(validationException),
            DomainException domainException => CreateDomainProblem(domainException),
            NotFoundException notFoundException => CreateNotFoundProblem(notFoundException),
            UnauthorizedAccessException => CreateUnauthorizedProblem(),
            _ => CreateInternalServerProblem(exception)
        };

        // Add correlation ID for debugging
        problemDetails.Extensions["correlationId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = problemDetails.Status ?? 500;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        });
    }

    private static ProblemDetails CreateValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Failed",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };
    }

    private static ProblemDetails CreateDomainProblem(DomainException exception)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Business Rule Violation",
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
        };
    }

    private static ProblemDetails CreateNotFoundProblem(NotFoundException exception)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Resource Not Found",
            Detail = exception.Message,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4"
        };
    }

    private static ProblemDetails CreateUnauthorizedProblem()
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "Authentication is required to access this resource",
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
        };
    }

    private static ProblemDetails CreateInternalServerProblem(Exception exception)
    {
        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = "An unexpected error occurred. Please try again later.",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };
    }
}
```

### Domain Exceptions

Create `src/Nastart.Domain/Common/Exceptions/DomainException.cs`:

```csharp
namespace Nastart.Domain.Common.Exceptions;

/// <summary>
/// Base exception for domain/business rule violations.
/// </summary>
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string message, string code = "DomainError") 
        : base(message)
    {
        Code = code;
    }

    public DomainException(string message, Exception innerException, string code = "DomainError")
        : base(message, innerException)
    {
        Code = code;
    }
}

/// <summary>
/// Exception for when a resource is not found.
/// </summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} with key '{key}' was not found.", "NotFound")
    {
    }
}

/// <summary>
/// Exception for business rule violations.
/// </summary>
public class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message)
        : base(message, "BusinessRuleViolation")
    {
    }
}
```

### Example ProblemDetails Response

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Validation Failed",
  "status": 400,
  "detail": null,
  "errors": {
    "Name": ["The Name field is required."],
    "CurrentPrice": ["Current price must be greater than 0."]
  },
  "correlationId": "0HNAB1234:00000001"
}
```

---

# Day 5: PaddleOCR Python Microservice

## 🧒 Explain Like I'm 5

**PaddleOCR** is like a robot that can read pictures 🤖📸:
- You show it a photo of a receipt
- It reads all the words
- It tells you what it found!

It's a separate helper (Python) that our main app (.NET) calls when needed.

## 🔧 Engineer Language

**PaddleOCR** is an open-source OCR library:
- Runs as a FastAPI microservice with proper layered architecture
- Uses dependency injection for testability
- Applies async patterns and performance optimizations
- Supports Indonesian language

> 📖 **Skill Reference**: `fastapi-templates` — *"Production-ready FastAPI projects with async patterns, dependency injection, middleware, and best practices."*

### Production Project Structure

Following the `fastapi-templates` skill recommended layout:

```
ocr-service/
├── app/
│   ├── api/
│   │   └── v1/
│   │       ├── endpoints/
│   │       │   └── receipts.py       # API route handlers
│   │       └── router.py             # API router
│   ├── core/
│   │   └── config.py                 # Pydantic Settings
│   ├── schemas/
│   │   └── ocr.py                    # Pydantic models
│   ├── services/
│   │   └── ocr_service.py            # Business logic
│   └── main.py                       # Application entry
├── tests/
│   ├── conftest.py
│   └── test_receipts.py
├── requirements.txt
├── Dockerfile
└── docker-compose.yml
```

### Core Configuration with Pydantic Settings

Create `ocr-service/app/core/config.py`:

```python
"""
Application configuration using Pydantic Settings.

Following fastapi-templates skill pattern:
- Environment variable loading with validation
- Cached settings with @lru_cache
- Type-safe configuration access
"""

from functools import lru_cache
from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    """Application settings loaded from environment."""
    
    # Service info
    APP_NAME: str = "Nastart OCR Service"
    APP_VERSION: str = "1.0.0"
    API_V1_PREFIX: str = "/api/v1"
    DEBUG: bool = False
    
    # OCR configuration
    OCR_LANGUAGE: str = "id"  # Indonesian
    OCR_USE_GPU: bool = False
    OCR_USE_ANGLE_CLS: bool = True
    
    # Performance settings
    MAX_IMAGE_SIZE_MB: int = 10
    ALLOWED_CONTENT_TYPES: str = "image/jpeg,image/png,image/webp"
    
    # Server settings
    HOST: str = "0.0.0.0"
    PORT: int = 8001
    
    class Config:
        env_file = ".env"
        case_sensitive = True
    
    @property
    def allowed_types(self) -> list[str]:
        """Parse allowed content types to list."""
        return self.ALLOWED_CONTENT_TYPES.split(",")


@lru_cache()
def get_settings() -> Settings:
    """
    Cached settings instance.
    
    Performance optimization: lru_cache ensures settings
    are only loaded once per process.
    """
    return Settings()
```

### Pydantic Schemas

Create `ocr-service/app/schemas/ocr.py`:

```python
"""
Pydantic schemas for OCR request/response models.

Following fastapi-templates skill:
- Strong typing for request/response
- Automatic validation and serialization
- OpenAPI schema generation
"""

from pydantic import BaseModel, Field
from typing import Optional


class TextLine(BaseModel):
    """A single line of text extracted by OCR."""
    
    text: str = Field(..., description="Extracted text content")
    confidence: float = Field(..., ge=0, le=1, description="OCR confidence score")
    bounding_box: list[list[float]] = Field(..., description="Text bounding box coordinates")


class OcrResponse(BaseModel):
    """Response from OCR scanning."""
    
    success: bool
    lines: list[TextLine] = Field(default_factory=list)
    raw_text: str = ""
    error: Optional[str] = None


class ExtractedItem(BaseModel):
    """An item extracted from receipt."""
    
    name: str
    price: float
    quantity: int = 1
    confidence: float


class ExtractItemsResponse(BaseModel):
    """Response from item extraction."""
    
    success: bool
    items: list[ExtractedItem] = Field(default_factory=list)
    total: Optional[float] = None
    raw_text: str = ""


class HealthResponse(BaseModel):
    """Health check response."""
    
    status: str
    service: str
    version: str
```

### OCR Service Layer

Create `ocr-service/app/services/ocr_service.py`:

```python
"""
OCR Service - Business logic layer.

Following fastapi-templates skill:
- Service layer separates business logic from routes
- Dependency injection for testability
- Single Responsibility Principle

Following python-performance-optimization skill:
- List comprehensions over loops (Pattern 5)
- Generator expressions for memory efficiency (Pattern 6)
- Local variable caching (Pattern 9)
"""

import re
import logging
from typing import Optional
from functools import lru_cache

import numpy as np
from PIL import Image
from paddleocr import PaddleOCR

from app.core.config import Settings, get_settings
from app.schemas.ocr import TextLine, OcrResponse, ExtractedItem, ExtractItemsResponse

logger = logging.getLogger(__name__)


class OcrService:
    """
    OCR service for receipt scanning.
    
    Implements service layer pattern from fastapi-templates skill.
    """
    
    # Compiled regex patterns (performance optimization)
    _PRICE_PATTERN = re.compile(r'([\d.,]+)\s*$')
    _NUMBER_PATTERN = re.compile(r'[\d.,]+')
    _TOTAL_KEYWORDS = frozenset(['total', 'jumlah', 'grand total', 'subtotal'])
    
    def __init__(self, settings: Settings):
        """
        Initialize OCR service with settings.
        
        Performance note: PaddleOCR model loading is expensive.
        Initialize once and reuse across requests.
        """
        self._settings = settings
        self._ocr: Optional[PaddleOCR] = None
    
    @property
    def ocr(self) -> PaddleOCR:
        """
        Lazy-loaded OCR engine.
        
        Performance optimization: Defer model loading until first use.
        """
        if self._ocr is None:
            logger.info("Initializing PaddleOCR engine...")
            self._ocr = PaddleOCR(
                use_angle_cls=self._settings.OCR_USE_ANGLE_CLS,
                lang=self._settings.OCR_LANGUAGE,
                use_gpu=self._settings.OCR_USE_GPU,
                show_log=False
            )
            logger.info("PaddleOCR engine initialized")
        return self._ocr
    
    async def scan_image(self, image_bytes: bytes, filename: str) -> OcrResponse:
        """
        Scan image and extract text.
        
        Args:
            image_bytes: Raw image bytes
            filename: Original filename for logging
        
        Returns:
            OcrResponse with extracted text lines
        """
        try:
            # Process image
            image = Image.open(__import__('io').BytesIO(image_bytes))
            
            # Convert to RGB if needed
            if image.mode != 'RGB':
                image = image.convert('RGB')
            
            # Convert to numpy array
            image_array = np.array(image)
            
            logger.info(f"Processing image: {filename}, size: {image.size}")
            
            # Run OCR (synchronous but fast)
            result = self.ocr.ocr(image_array, cls=True)
            
            if not result or not result[0]:
                return OcrResponse(
                    success=True,
                    lines=[],
                    raw_text="",
                    error="No text detected in image"
                )
            
            # Extract lines using list comprehension (Pattern 5)
            # More efficient than loop with append
            lines = [
                TextLine(
                    text=line[1][0],
                    confidence=float(line[1][1]),
                    bounding_box=line[0]
                )
                for line in result[0]
            ]
            
            # Join using generator (Pattern 6 - memory efficient)
            raw_text = "\n".join(line.text for line in lines)
            
            logger.info(f"Extracted {len(lines)} lines from {filename}")
            
            return OcrResponse(
                success=True,
                lines=lines,
                raw_text=raw_text
            )
            
        except Exception as e:
            logger.error(f"OCR error: {str(e)}")
            return OcrResponse(
                success=False,
                lines=[],
                raw_text="",
                error=f"OCR processing failed: {str(e)}"
            )
    
    async def extract_items(self, image_bytes: bytes, filename: str) -> ExtractItemsResponse:
        """
        Extract structured items from receipt image.
        
        Uses heuristics to parse item names and prices.
        """
        # First scan the image
        ocr_result = await self.scan_image(image_bytes, filename)
        
        if not ocr_result.success or not ocr_result.lines:
            return ExtractItemsResponse(
                success=False,
                items=[],
                total=None,
                raw_text=ocr_result.raw_text
            )
        
        items: list[ExtractedItem] = []
        total: Optional[float] = None
        
        # Cache patterns locally (Pattern 9 - faster access)
        price_pattern = self._PRICE_PATTERN
        number_pattern = self._NUMBER_PATTERN
        total_keywords = self._TOTAL_KEYWORDS
        
        for line in ocr_result.lines:
            text = line.text.strip()
            
            # Skip short lines
            if len(text) < 3:
                continue
            
            text_lower = text.lower()
            
            # Check for total line
            if any(kw in text_lower for kw in total_keywords):
                numbers = number_pattern.findall(text)
                if numbers:
                    total = self._parse_indonesian_price(numbers[-1])
                continue
            
            # Try to extract item with price
            price_match = price_pattern.search(text)
            if price_match:
                price = self._parse_indonesian_price(price_match.group(1))
                if price is not None:
                    name = text[:price_match.start()].strip()
                    if name:
                        items.append(ExtractedItem(
                            name=name,
                            price=price,
                            quantity=1,
                            confidence=line.confidence
                        ))
        
        return ExtractItemsResponse(
            success=True,
            items=items,
            total=total,
            raw_text=ocr_result.raw_text
        )
    
    @staticmethod
    def _parse_indonesian_price(price_str: str) -> Optional[float]:
        """
        Parse Indonesian price format.
        
        Indonesian format: 1.000.000 = 1,000,000
        """
        try:
            # Remove thousand separators (.) and convert decimal separator
            cleaned = price_str.replace('.', '').replace(',', '.')
            return float(cleaned)
        except ValueError:
            return None


@lru_cache()
def get_ocr_service() -> OcrService:
    """
    Dependency injection for OCR service.
    
    Cached to ensure single instance per process.
    """
    return OcrService(get_settings())
```

### API Endpoints with Dependency Injection

Create `ocr-service/app/api/v1/endpoints/receipts.py`:

```python
"""
Receipt scanning API endpoints.

Following fastapi-templates skill:
- Thin route handlers (delegate to service layer)
- Dependency injection with Depends
- Proper error handling with HTTPException
- OpenAPI documentation with docstrings
"""

import logging
from fastapi import APIRouter, Depends, HTTPException, UploadFile, status

from app.core.config import Settings, get_settings
from app.schemas.ocr import OcrResponse, ExtractItemsResponse, HealthResponse
from app.services.ocr_service import OcrService, get_ocr_service

logger = logging.getLogger(__name__)

router = APIRouter()


@router.get("/health", response_model=HealthResponse)
async def health_check(settings: Settings = Depends(get_settings)):
    """
    Health check endpoint.
    
    Returns service status and version information.
    """
    return HealthResponse(
        status="healthy",
        service=settings.APP_NAME,
        version=settings.APP_VERSION
    )


@router.post(
    "/scan-receipt",
    response_model=OcrResponse,
    status_code=status.HTTP_200_OK,
    summary="Scan receipt image",
    description="Extract text from receipt image using PaddleOCR"
)
async def scan_receipt(
    file: UploadFile,
    settings: Settings = Depends(get_settings),
    ocr_service: OcrService = Depends(get_ocr_service)
):
    """
    Scan a receipt image and extract text.
    
    Args:
        file: Image file (JPEG, PNG, WebP)
    
    Returns:
        Extracted text lines with confidence scores
    
    Raises:
        HTTPException 400: Invalid file type
        HTTPException 413: File too large
    """
    # Validate file type
    if file.content_type not in settings.allowed_types:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Invalid file type. Allowed: {settings.allowed_types}"
        )
    
    # Read file content
    contents = await file.read()
    
    # Validate file size
    max_size = settings.MAX_IMAGE_SIZE_MB * 1024 * 1024
    if len(contents) > max_size:
        raise HTTPException(
            status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
            detail=f"File too large. Max size: {settings.MAX_IMAGE_SIZE_MB}MB"
        )
    
    # Delegate to service layer
    result = await ocr_service.scan_image(contents, file.filename or "unknown")
    
    if not result.success and result.error:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail=result.error
        )
    
    return result


@router.post(
    "/extract-items",
    response_model=ExtractItemsResponse,
    status_code=status.HTTP_200_OK,
    summary="Extract items from receipt",
    description="Scan receipt and extract structured item data"
)
async def extract_items(
    file: UploadFile,
    settings: Settings = Depends(get_settings),
    ocr_service: OcrService = Depends(get_ocr_service)
):
    """
    Scan receipt and extract structured items.
    
    Uses heuristics to parse item names, quantities, and prices.
    """
    # Validate file type
    if file.content_type not in settings.allowed_types:
        raise HTTPException(
            status_code=status.HTTP_400_BAD_REQUEST,
            detail=f"Invalid file type. Allowed: {settings.allowed_types}"
        )
    
    contents = await file.read()
    
    # Validate file size
    max_size = settings.MAX_IMAGE_SIZE_MB * 1024 * 1024
    if len(contents) > max_size:
        raise HTTPException(
            status_code=status.HTTP_413_REQUEST_ENTITY_TOO_LARGE,
            detail=f"File too large. Max size: {settings.MAX_IMAGE_SIZE_MB}MB"
        )
    
    return await ocr_service.extract_items(contents, file.filename or "unknown")
```

### API Router

Create `ocr-service/app/api/v1/router.py`:

```python
"""
API v1 router aggregation.

Following fastapi-templates skill:
- Centralized router for API version
- Clean separation of concerns
"""

from fastapi import APIRouter
from app.api.v1.endpoints import receipts

api_router = APIRouter()

api_router.include_router(
    receipts.router,
    prefix="/receipts",
    tags=["Receipts"]
)
```

### Application Entry Point

Create `ocr-service/app/main.py`:

```python
"""
FastAPI Application Entry Point.

Following fastapi-templates skill:
- Lifespan context manager for startup/shutdown
- Proper middleware configuration
- Clean separation of concerns
"""

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.core.config import get_settings
from app.api.v1.router import api_router
from app.services.ocr_service import get_ocr_service

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s"
)
logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    Application lifespan events.
    
    Following fastapi-templates skill pattern:
    - Startup: Initialize expensive resources
    - Shutdown: Cleanup resources
    """
    # Startup
    logger.info("Starting OCR service...")
    settings = get_settings()
    
    # Pre-warm OCR engine (expensive initialization)
    # This prevents first request from being slow
    ocr_service = get_ocr_service()
    _ = ocr_service.ocr  # Trigger lazy loading
    
    logger.info(f"OCR service started: {settings.APP_NAME} v{settings.APP_VERSION}")
    
    yield
    
    # Shutdown
    logger.info("Shutting down OCR service...")


def create_app() -> FastAPI:
    """
    Application factory pattern.
    
    Enables easier testing and configuration.
    """
    settings = get_settings()
    
    app = FastAPI(
        title=settings.APP_NAME,
        version=settings.APP_VERSION,
        description="Receipt scanning microservice using PaddleOCR",
        lifespan=lifespan,
        docs_url="/docs",
        redoc_url="/redoc"
    )
    
    # CORS middleware
    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )
    
    # Include API router
    app.include_router(api_router, prefix=settings.API_V1_PREFIX)
    
    # Root health check (outside versioned API)
    @app.get("/health")
    async def root_health():
        return {"status": "healthy"}
    
    return app


# Create application instance
app = create_app()


if __name__ == "__main__":
    import uvicorn
    settings = get_settings()
    uvicorn.run(
        "app.main:app",
        host=settings.HOST,
        port=settings.PORT,
        reload=settings.DEBUG
    )
```

### Tests

Create `ocr-service/tests/conftest.py`:

```python
"""
Test fixtures following fastapi-templates skill.
"""

import pytest
from fastapi.testclient import TestClient
from unittest.mock import MagicMock, patch

from app.main import create_app
from app.services.ocr_service import OcrService, get_ocr_service


@pytest.fixture
def mock_ocr_service():
    """Mock OCR service for testing."""
    mock = MagicMock(spec=OcrService)
    return mock


@pytest.fixture
def client(mock_ocr_service):
    """Test client with mocked dependencies."""
    app = create_app()
    
    # Override dependency
    app.dependency_overrides[get_ocr_service] = lambda: mock_ocr_service
    
    with TestClient(app) as client:
        yield client
    
    # Cleanup
    app.dependency_overrides.clear()
```

Create `ocr-service/tests/test_receipts.py`:

```python
"""
Receipt endpoint tests.
"""

import pytest
from io import BytesIO

from app.schemas.ocr import OcrResponse, TextLine


def test_health_check(client):
    """Test health endpoint returns 200."""
    response = client.get("/api/v1/receipts/health")
    assert response.status_code == 200
    data = response.json()
    assert data["status"] == "healthy"


def test_scan_receipt_invalid_type(client):
    """Test invalid file type returns 400."""
    file_content = b"not an image"
    response = client.post(
        "/api/v1/receipts/scan-receipt",
        files={"file": ("test.txt", BytesIO(file_content), "text/plain")}
    )
    assert response.status_code == 400


def test_scan_receipt_success(client, mock_ocr_service):
    """Test successful receipt scan."""
    # Setup mock
    mock_ocr_service.scan_image.return_value = OcrResponse(
        success=True,
        lines=[
            TextLine(text="Item 1", confidence=0.95, bounding_box=[[0, 0], [1, 0], [1, 1], [0, 1]])
        ],
        raw_text="Item 1"
    )
    
    # Create test image (1x1 pixel PNG)
    import base64
    png_data = base64.b64decode(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
    )
    
    response = client.post(
        "/api/v1/receipts/scan-receipt",
        files={"file": ("test.png", BytesIO(png_data), "image/png")}
    )
    
    assert response.status_code == 200
    data = response.json()
    assert data["success"] is True
```

### Requirements

Create `ocr-service/requirements.txt`:

```
# FastAPI framework
fastapi>=0.115.0
uvicorn[standard]>=0.32.0
pydantic>=2.0.0
pydantic-settings>=2.0.0

# OCR
paddlepaddle>=2.6.0
paddleocr>=2.8.0

# Image processing
Pillow>=10.0.0
numpy>=1.24.0

# File upload handling
python-multipart>=0.0.9

# Testing
pytest>=8.0.0
pytest-asyncio>=0.23.0
httpx>=0.27.0
```

### Dockerfile (Production-Optimized)

Create `ocr-service/Dockerfile`:

```dockerfile
# Multi-stage build for smaller image
# Following python-performance-optimization skill best practices
FROM python:3.11-slim as builder

WORKDIR /app

# Install build dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# Install Python dependencies to user directory
COPY requirements.txt .
RUN pip install --no-cache-dir --user -r requirements.txt

# Production stage
FROM python:3.11-slim

WORKDIR /app

# Install runtime dependencies for PaddleOCR
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgl1-mesa-glx \
    libglib2.0-0 \
    libsm6 \
    libxrender1 \
    libxext6 \
    && rm -rf /var/lib/apt/lists/*

# Copy installed packages from builder
COPY --from=builder /root/.local /root/.local
ENV PATH=/root/.local/bin:$PATH

# Copy application code
COPY app/ ./app/

# Create non-root user for security
RUN useradd --create-home appuser
USER appuser

# Expose port
EXPOSE 8001

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8001/health || exit 1

# Run the service with uvicorn
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8001"]
```

### Create docker-compose.yml

Create `ocr-service/docker-compose.yml`:

```yaml
version: '3.8'

services:
  ocr-service:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8001:8001"
    environment:
      - PYTHONUNBUFFERED=1
      - OCR_USE_GPU=false
      - DEBUG=false
      - MAX_IMAGE_SIZE_MB=10
    volumes:
      # Mount for development hot-reload
      - ./app:/app/app:ro
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8001/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s  # OCR model takes time to load
    restart: unless-stopped
    
  # Development only: auto-reload
  ocr-service-dev:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8001:8001"
    environment:
      - PYTHONUNBUFFERED=1
      - DEBUG=true
    volumes:
      - ./app:/app/app
    command: uvicorn app.main:app --host 0.0.0.0 --port 8001 --reload
    profiles:
      - dev
```

### Environment File

Create `ocr-service/.env.example`:

```env
# Application
APP_NAME=Nastart OCR Service
APP_VERSION=1.0.0
DEBUG=false

# OCR Configuration
OCR_LANGUAGE=id
OCR_USE_GPU=false
OCR_USE_ANGLE_CLS=true

# Performance
MAX_IMAGE_SIZE_MB=10
ALLOWED_CONTENT_TYPES=image/jpeg,image/png,image/webp

# Server
HOST=0.0.0.0
PORT=8001
```

### Run the OCR Service

```powershell
cd ocr-service

# Option 1: Run directly with Python (development)
pip install -r requirements.txt
python -m app.main

# Option 2: Run with uvicorn directly
uvicorn app.main:app --host 0.0.0.0 --port 8001 --reload

# Option 3: Run with Docker (production)
docker-compose up --build

# Option 4: Run with Docker (development with hot-reload)
docker-compose --profile dev up --build
```

### Performance Optimizations Applied

Following the `python-performance-optimization` skill patterns:

| Pattern | Applied In | Benefit |
|---------|-----------|---------|
| **Pattern 5**: List Comprehensions | `OcrService.scan_image()` | Faster than loop with append |
| **Pattern 6**: Generator Expressions | `"\n".join(line.text for line in lines)` | Memory efficient for large texts |
| **Pattern 9**: Local Variable Caching | `extract_items()` caches regex patterns | Faster repeated access |
| **Pattern 12**: `@lru_cache` | `get_settings()`, `get_ocr_service()` | Single instance per process |
| **Lazy Loading** | `OcrService.ocr` property | Defer expensive model loading |
| **Compiled Regex** | Class-level `_PRICE_PATTERN` | Avoid recompilation per call |

---

# Day 6: HttpClient & OCR Service Integration

## 🧒 Explain Like I'm 5

When our .NET app needs help from Python's OCR service, it makes a phone call 📞:
1. .NET: "Hey Python, can you read this receipt?"
2. Python: *reads the image*
3. Python: "Here's what I found!"

**HttpClient** is the phone that makes these calls.

## 🔧 Engineer Language

**IHttpClientFactory** manages `HttpClient` instances:
- Prevents socket exhaustion
- Enables resilience policies (retry, timeout)
- Typed clients for clean abstractions

> 📖 **Microsoft Docs**: *"IHttpClientFactory can be used to configure and create HttpClient instances. It handles the lifecycle of HttpMessageHandler instances."*
>
> — [Make HTTP requests](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests)

### IOcrService Interface

Create `src/Nastart.Application/Common/Interfaces/IOcrService.cs`:

```csharp
namespace Nastart.Application.Common.Interfaces;

/// <summary>
/// Interface for OCR (Optical Character Recognition) service.
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Scans an image and extracts text.
    /// </summary>
    Task<OcrResult> ScanImageAsync(byte[] imageBytes, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of OCR scanning.
/// </summary>
public record OcrResult(
    bool Success,
    IReadOnlyList<OcrLine> Lines,
    string RawText,
    string? Error = null
);

/// <summary>
/// A line of text extracted by OCR.
/// </summary>
public record OcrLine(
    string Text,
    double Confidence
);
```

### PaddleOcrService Implementation

Create `src/Nastart.Infrastructure/ExternalServices/PaddleOcrService.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Infrastructure.ExternalServices;

/// <summary>
/// HTTP client for PaddleOCR microservice.
/// </summary>
/// <remarks>
/// Following Microsoft's HttpClient patterns:
/// - Typed client with IHttpClientFactory
/// - Proper error handling
/// - Structured logging
/// 
/// See: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests
/// </remarks>
public class PaddleOcrService : IOcrService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaddleOcrService> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public PaddleOcrService(HttpClient httpClient, ILogger<PaddleOcrService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OcrResult> ScanImageAsync(
        byte[] imageBytes, 
        string fileName, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending image to OCR service: {FileName}, Size: {Size} bytes", 
                fileName, imageBytes.Length);

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                GetContentType(fileName));
            
            content.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync("/scan-receipt", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("OCR service error: {StatusCode} - {Error}", 
                    response.StatusCode, errorBody);
                
                return new OcrResult(
                    Success: false,
                    Lines: Array.Empty<OcrLine>(),
                    RawText: string.Empty,
                    Error: $"OCR service returned {response.StatusCode}"
                );
            }

            var result = await response.Content.ReadFromJsonAsync<OcrApiResponse>(
                _jsonOptions, cancellationToken);

            if (result is null)
            {
                return new OcrResult(
                    Success: false,
                    Lines: Array.Empty<OcrLine>(),
                    RawText: string.Empty,
                    Error: "Invalid response from OCR service"
                );
            }

            var lines = result.Lines
                .Select(l => new OcrLine(l.Text, l.Confidence))
                .ToList();

            _logger.LogInformation("OCR extracted {LineCount} lines from {FileName}", 
                lines.Count, fileName);

            return new OcrResult(
                Success: result.Success,
                Lines: lines,
                RawText: result.RawText,
                Error: result.Error
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to OCR service");
            return new OcrResult(
                Success: false,
                Lines: Array.Empty<OcrLine>(),
                RawText: string.Empty,
                Error: "OCR service is unavailable"
            );
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "OCR service timeout");
            return new OcrResult(
                Success: false,
                Lines: Array.Empty<OcrLine>(),
                RawText: string.Empty,
                Error: "OCR service timeout"
            );
        }
    }

    private static string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}

// API response models
internal record OcrApiResponse(
    bool Success,
    List<OcrApiLine> Lines,
    [property: JsonPropertyName("raw_text")] string RawText,
    string? Error
);

internal record OcrApiLine(
    string Text,
    double Confidence,
    [property: JsonPropertyName("bounding_box")] List<List<double>> BoundingBox
);
```

### Register HttpClient in DependencyInjection

Update `src/Nastart.Infrastructure/DependencyInjection.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nastart.Application.Common.Interfaces;
using Nastart.Infrastructure.ExternalServices;
using Nastart.Infrastructure.Persistence;
using Nastart.Infrastructure.Persistence.Repositories;

namespace Nastart.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ... existing DbContext and repository registrations ...

        // Register OCR service with typed HttpClient
        services.AddHttpClient<IOcrService, PaddleOcrService>(client =>
        {
            var baseUrl = configuration["OcrService:BaseUrl"] ?? "http://localhost:8001";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            // Allow self-signed certs in development
            ServerCertificateCustomValidationCallback = 
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        return services;
    }
}
```

### Update appsettings.json

Update `src/Nastart.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=nastart;Username=postgres;Password=postgres"
  },
  "OcrService": {
    "BaseUrl": "http://localhost:8001"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Nastart.Infrastructure.ExternalServices": "Debug"
    }
  },
  "AllowedHosts": "*"
}
```

### ScanReceiptCommand Handler

Create `src/Nastart.Application/Finance/Commands/ScanReceipt/ScanReceiptCommand.cs`:

```csharp
using MediatR;
using Nastart.Application.Common;
using Nastart.Application.Common.Interfaces;
using Nastart.Api.Contracts.Responses;

namespace Nastart.Application.Finance.Commands.ScanReceipt;

/// <summary>
/// Command to scan a receipt image and extract items.
/// </summary>
public record ScanReceiptCommand(
    byte[] ImageBytes,
    string FileName
) : IRequest<Result<ReceiptScanResponse>>;

/// <summary>
/// Handler for ScanReceiptCommand.
/// </summary>
public class ScanReceiptCommandHandler : IRequestHandler<ScanReceiptCommand, Result<ReceiptScanResponse>>
{
    private readonly IOcrService _ocrService;
    private readonly IIngredientRepository _ingredientRepository;
    private readonly ILogger<ScanReceiptCommandHandler> _logger;

    public ScanReceiptCommandHandler(
        IOcrService ocrService,
        IIngredientRepository ingredientRepository,
        ILogger<ScanReceiptCommandHandler> logger)
    {
        _ocrService = ocrService;
        _ingredientRepository = ingredientRepository;
        _logger = logger;
    }

    public async Task<Result<ReceiptScanResponse>> Handle(
        ScanReceiptCommand request, 
        CancellationToken cancellationToken)
    {
        // 1. Scan image with OCR
        var ocrResult = await _ocrService.ScanImageAsync(
            request.ImageBytes, 
            request.FileName, 
            cancellationToken);

        if (!ocrResult.Success)
        {
            return Result<ReceiptScanResponse>.Failure(
                Error.Failure("OcrFailed", ocrResult.Error ?? "OCR scanning failed"));
        }

        // 2. Match OCR lines to known ingredients
        var matchedItems = new List<ReceiptItemResponse>();
        var unmatchedLines = new List<string>();

        var allIngredients = await _ingredientRepository.GetAllAsync(cancellationToken);

        foreach (var line in ocrResult.Lines)
        {
            var matchedIngredient = FindMatchingIngredient(line.Text, allIngredients);
            
            if (matchedIngredient is not null)
            {
                // Extract price from line (heuristic)
                var price = ExtractPrice(line.Text);
                
                matchedItems.Add(new ReceiptItemResponse(
                    IngredientId: matchedIngredient.Id.Value,
                    IngredientName: matchedIngredient.Name,
                    DetectedText: line.Text,
                    DetectedPrice: price,
                    Confidence: line.Confidence
                ));
            }
            else if (line.Text.Length > 3)
            {
                unmatchedLines.Add(line.Text);
            }
        }

        _logger.LogInformation(
            "Receipt scan: {MatchedCount} items matched, {UnmatchedCount} lines unmatched",
            matchedItems.Count, unmatchedLines.Count);

        return Result<ReceiptScanResponse>.Success(new ReceiptScanResponse(
            Success: true,
            MatchedItems: matchedItems,
            UnmatchedLines: unmatchedLines,
            RawText: ocrResult.RawText
        ));
    }

    private static Ingredient? FindMatchingIngredient(string text, IEnumerable<Ingredient> ingredients)
    {
        var lowerText = text.ToLowerInvariant();
        
        // Try exact match first
        var exactMatch = ingredients.FirstOrDefault(i => 
            lowerText.Contains(i.Name.ToLowerInvariant()));
        
        if (exactMatch is not null)
            return exactMatch;

        // TODO: Add fuzzy matching for better results
        return null;
    }

    private static decimal? ExtractPrice(string text)
    {
        // Simple regex to find price at end of line
        var match = System.Text.RegularExpressions.Regex.Match(
            text, @"([\d.,]+)\s*$");
        
        if (match.Success)
        {
            var priceStr = match.Groups[1].Value
                .Replace(".", "")
                .Replace(",", ".");
            
            if (decimal.TryParse(priceStr, out var price))
                return price;
        }

        return null;
    }
}
```

### Receipt Response DTO

Create `src/Nastart.Api/Contracts/Responses/ReceiptScanResponse.cs`:

```csharp
namespace Nastart.Api.Contracts.Responses;

/// <summary>
/// Response from receipt scanning.
/// </summary>
public record ReceiptScanResponse(
    bool Success,
    IReadOnlyList<ReceiptItemResponse> MatchedItems,
    IReadOnlyList<string> UnmatchedLines,
    string RawText
);

/// <summary>
/// An item detected in a receipt.
/// </summary>
public record ReceiptItemResponse(
    Guid IngredientId,
    string IngredientName,
    string DetectedText,
    decimal? DetectedPrice,
    double Confidence
);
```

---

# Day 7: OpenAPI Documentation & API Versioning

## 🧒 Explain Like I'm 5

**API documentation** is like a recipe book 📖 for your API:
- It tells other programmers what your API can do
- Shows them the right way to use it
- Swagger UI lets them try it out!

## 🔧 Engineer Language

**OpenAPI/Swagger** provides:
- Machine-readable API specification
- Interactive testing UI
- Auto-generated client SDKs
- Consistent documentation

> 📖 **Microsoft Docs**: *"Swagger metadata generation enables OpenAPI documentation for ASP.NET Core Web API applications."*
>
> — [Get started with Swashbuckle](https://learn.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle)

### Enhanced Swagger Configuration

Update `src/Nastart.Api/Program.cs`:

```csharp
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

// In ConfigureServices section:
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
    
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Nastart API",
        Description = "Smart inventory & finance tracking API for small F&B businesses",
        Contact = new OpenApiContact
        {
            Name = "Nastart Support",
            Email = "support@nastart.app",
            Url = new Uri("https://nastart.app")
        },
        License = new OpenApiLicense
        {
            Name = "MIT",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Include XML comments
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Add security definition for future auth
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // Operation filter for response examples
    options.OperationFilter<DefaultResponsesOperationFilter>();
});
```

### Enable XML Documentation

Update `src/Nastart.Api/Nastart.Api.csproj`:

```xml
<PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

### API Versioning

For larger APIs, implement versioning:

```powershell
dotnet add src/Nastart.Api/Nastart.Api.csproj package Asp.Versioning.Mvc
dotnet add src/Nastart.Api/Nastart.Api.csproj package Asp.Versioning.Mvc.ApiExplorer
```

Configure versioning:

```csharp
// In Program.cs
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version")
    );
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
```

### URI Versioning Pattern

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class RecipesController : ControllerBase
{
    // v1 endpoints
}

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("2.0")]
public class RecipesV2Controller : ControllerBase
{
    // v2 endpoints with breaking changes
}
```

> 📖 **Microsoft Docs**: *"Versioning enables a Web API to indicate the features and resources that it exposes. A client application can then submit requests to a specific version."*
>
> — [Versioning a RESTful web API](https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design#versioning-a-restful-web-api)

### DefaultResponsesOperationFilter

Create `src/Nastart.Api/Extensions/DefaultResponsesOperationFilter.cs`:

```csharp
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Nastart.Api.Extensions;

/// <summary>
/// Adds common response types to all operations.
/// </summary>
public class DefaultResponsesOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Add common error responses
        operation.Responses.TryAdd("401", new OpenApiResponse 
        { 
            Description = "Unauthorized" 
        });
        
        operation.Responses.TryAdd("500", new OpenApiResponse 
        { 
            Description = "Internal Server Error",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new OpenApiMediaType
                {
                    Schema = context.SchemaGenerator.GenerateSchema(
                        typeof(Microsoft.AspNetCore.Mvc.ProblemDetails), 
                        context.SchemaRepository)
                }
            }
        });
    }
}
```

---

# Resources

## Microsoft Official Documentation (Verified via MCP)

> 📖 **Skill Reference**: `microsoft-docs` — *"Query official Microsoft documentation to understand concepts, find tutorials, and learn how services work."*

| Topic | Link |
|-------|------|
| Implement the command process pipeline with MediatR | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api |
| Minimal APIs quick reference | https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis |
| Create responses in Minimal API apps | https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses |
| Handle errors in ASP.NET Core | https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling |
| Make HTTP requests using IHttpClientFactory | https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests |
| Get started with Swashbuckle | https://learn.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle |
| Versioning a RESTful web API | https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design#versioning-a-restful-web-api |

## Skills Applied

> 📖 **Skill Reference**: `microsoft-code-reference` — *"Look up Microsoft API references, find working code samples, and verify SDK code is correct."*

| Skill | Applied To | Patterns Used |
|-------|-----------|---------------|
| `fastapi-templates` | OCR Microservice | Lifespan, DI with Depends, Service Layer, Pydantic Settings |
| `python-performance-optimization` | OCR Service | List comprehensions, generators, lru_cache, compiled regex |
| `microsoft-docs` | .NET API Layer | Controllers, MediatR, ProblemDetails, HttpClientFactory |
| `microsoft-code-reference` | Code Samples | Verified patterns from eShopOnContainers |

## Folder Structure After Week 7

```
src/Nastart.Api/
├── Controllers/
│   ├── IngredientsController.cs                ✅
│   ├── RecipesController.cs                    ✅
│   └── PurchasesController.cs                  ✅
├── Endpoints/
│   ├── CategoryEndpoints.cs                    ✅
│   └── ReceiptEndpoints.cs                     ✅
├── Middleware/
│   └── GlobalExceptionHandler.cs               ✅
├── Contracts/
│   ├── Requests/
│   │   ├── CreateIngredientRequest.cs          ✅
│   │   ├── CreateRecipeRequest.cs              ✅
│   │   └── AddIngredientToRecipeRequest.cs     ✅
│   └── Responses/
│       ├── IngredientResponse.cs               ✅
│       ├── RecipeResponse.cs                   ✅
│       └── ReceiptScanResponse.cs              ✅
├── Extensions/
│   └── DefaultResponsesOperationFilter.cs      ✅
├── Program.cs                                  ✅ (updated)
└── appsettings.json                            ✅ (updated)

src/Nastart.Application/
├── Common/
│   └── Interfaces/
│       └── IOcrService.cs                      ✅
└── Finance/
    └── Commands/
        └── ScanReceipt/
            └── ScanReceiptCommand.cs           ✅

src/Nastart.Infrastructure/
├── ExternalServices/
│   └── PaddleOcrService.cs                     ✅
└── DependencyInjection.cs                      ✅ (updated)

src/Nastart.Domain/
└── Common/
    └── Exceptions/
        ├── DomainException.cs                  ✅
        └── NotFoundException.cs                ✅

ocr-service/                                    # Production FastAPI structure
├── app/
│   ├── api/
│   │   └── v1/
│   │       ├── endpoints/
│   │       │   └── receipts.py                 ✅
│   │       └── router.py                       ✅
│   ├── core/
│   │   └── config.py                           ✅ (Pydantic Settings)
│   ├── schemas/
│   │   └── ocr.py                              ✅ (Pydantic models)
│   ├── services/
│   │   └── ocr_service.py                      ✅ (Business logic)
│   └── main.py                                 ✅ (Lifespan + Factory)
├── tests/
│   ├── conftest.py                             ✅
│   └── test_receipts.py                        ✅
├── requirements.txt                            ✅
├── Dockerfile                                  ✅ (Multi-stage)
├── docker-compose.yml                          ✅
└── .env.example                                ✅
```

## Week 7 Checklist

- [x] Install Swashbuckle for OpenAPI/Swagger
- [x] Create IngredientsController with MediatR
- [x] Create RecipesController with MediatR
- [x] Create Minimal API endpoints (Categories, Receipts)
- [x] Implement GlobalExceptionHandler with ProblemDetails
- [x] Create domain exceptions (DomainException, NotFoundException)
- [x] Create PaddleOCR Python microservice with FastAPI *(refactored with skills)*
- [x] Implement IOcrService interface
- [x] Implement PaddleOcrService with IHttpClientFactory
- [x] Create ScanReceiptCommand handler
- [x] Configure Swagger with annotations
- [x] Set up API versioning pattern

### Python Microservice Patterns Applied

| Pattern from `fastapi-templates` | Implementation |
|----------------------------------|----------------|
| Lifespan context manager | `app/main.py` — Pre-warm OCR engine |
| Pydantic Settings | `app/core/config.py` — Environment-based config |
| Service Layer | `app/services/ocr_service.py` — Business logic separated |
| Dependency Injection | `Depends(get_ocr_service)` in endpoints |
| Application Factory | `create_app()` for testability |
| Repository-like pattern | `OcrService` encapsulates PaddleOCR |

| Pattern from `python-performance-optimization` | Implementation |
|-----------------------------------------------|----------------|
| Pattern 5: List Comprehensions | `scan_image()` — Build lines list |
| Pattern 6: Generator Expressions | Join raw_text with generator |
| Pattern 9: Local Variable Caching | Cache regex patterns in method |
| Pattern 12: `@lru_cache` | Singleton services and settings |
| Lazy Loading | `OcrService.ocr` property |
| Compiled Regex | Class-level `_PRICE_PATTERN` |

---

## Next Week Preview: Week 8 — Telegram Bot Fundamentals

In Week 8, we'll build the Telegram Bot:
- **Bot creation** via BotFather
- **Webhook** setup for receiving updates
- **Command routing** for `/start`, `/help`, `/cost`
- **Photo handling** for receipt uploads
- **Integration** with our OCR service

---

*Last Updated: Week 7 Lesson*
*Stack: .NET 10, ASP.NET Core, MediatR, FastAPI, PaddleOCR*
*Skills: fastapi-templates, python-performance-optimization, microsoft-docs, microsoft-code-reference*
