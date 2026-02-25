# Week 6: API Polish & OpenAPI 🎨

> **Goal**: Polish your API with OpenAPI documentation, health checks, CORS configuration, rate limiting, and robust error handling middleware — making your API production-ready.

---

## Table of Contents
1. [Day 1: OpenAPI/Swagger Configuration](#day-1-openapiswagger-configuration)
2. [Day 2: Request/Response Examples & Schemas](#day-2-requestresponse-examples--schemas)
3. [Day 3: Health Check Endpoints](#day-3-health-check-endpoints)
4. [Day 4: CORS Configuration for Frontend](#day-4-cors-configuration-for-frontend)
5. [Day 5: Rate Limiting for Bot Endpoints](#day-5-rate-limiting-for-bot-endpoints)
6. [Day 6: Global Error Handling Middleware](#day-6-global-error-handling-middleware)
7. [Day 7: API Testing & Documentation Review](#day-7-api-testing--documentation-review)
8. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1: OpenAPI/Swagger Configuration

## 🧒 Explain Like I'm 5

Imagine you build a toy robot that can do lots of tricks. But when your friends come over, they don't know how to make it work! So you write a **manual** 📖 that says:
- "Press the red button to make it walk"
- "Press the blue button to make it dance"
- "Say 'hello' and it says 'hi' back"

**OpenAPI (Swagger)** is like that manual — but for your API! It shows everyone:
- What endpoints exist
- What data to send
- What responses come back
- How to try it out!

## 🔧 Engineer Language

**OpenAPI** (formerly Swagger) is a specification for describing RESTful APIs. ASP.NET Core provides built-in support for generating OpenAPI documents with Swagger UI for interactive testing.

> 📖 **Microsoft Docs**: *"OpenAPI provides a standardized way to document HTTP APIs. Swagger UI provides a web-based UI to interact with the API using the document."*
>
> — [ASP.NET Core OpenAPI documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview)

### Install Required Packages:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# OpenAPI support (built into .NET 9+)
dotnet add package Swashbuckle.AspNetCore

# For API versioning
dotnet add package Asp.Versioning.Http
```

### Configure OpenAPI in Program.cs:

```csharp
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// ══════════════════════════════════════════════════════════════
// OPENAPI / SWAGGER CONFIGURATION
// ══════════════════════════════════════════════════════════════

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Nastart API",
        Description = """
            Smart inventory & finance tracking API for small F&B businesses.
            
            ## Features
            - 📦 **Ingredients** — Track inventory with price history
            - 🍳 **Recipes** — Calculate real-time costs and margins
            - 🧾 **Purchases** — Record purchases via OCR receipt scanning
            - 🔔 **Alerts** — Price spikes, low stock, margin warnings
            
            ## Authentication
            Currently uses user ID passed in requests. JWT authentication coming soon.
            """,
        Contact = new OpenApiContact
        {
            Name = "Nastart Support",
            Email = "support@nastart.app",
            Url = new Uri("https://nastart.app")
        },
        License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });
    
    // Include XML comments from assembly
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
    
    // Add security definition for future JWT
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    
    // Custom schema IDs to avoid conflicts
    options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
    
    // Tag descriptions for grouping endpoints
    options.TagActionsBy(api =>
    {
        if (api.GroupName != null)
            return new[] { api.GroupName };
        
        // Extract tag from route template
        var routeTemplate = api.RelativePath ?? "";
        var firstSegment = routeTemplate.Split('/').FirstOrDefault(s => s != "api");
        return new[] { firstSegment ?? "General" };
    });
});

var app = builder.Build();

// ══════════════════════════════════════════════════════════════
// SWAGGER UI MIDDLEWARE
// ══════════════════════════════════════════════════════════════

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "api-docs/{documentName}/swagger.json";
    });
    
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/api-docs/v1/swagger.json", "Nastart API v1");
        options.RoutePrefix = "swagger";
        
        // UI customization
        options.DocumentTitle = "Nastart API Documentation";
        options.DefaultModelsExpandDepth(2);
        options.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        options.EnableDeepLinking();
        options.EnableFilter();
        options.ShowExtensions();
        
        // Try it out enabled by default
        options.EnableTryItOutByDefault();
    });
}

// ... rest of middleware and endpoint mappings
```

### Enable XML Documentation:

Update `Nastart.Api.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    
    <!-- Enable XML documentation for Swagger -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <NoWarn>$(NoWarn);1591</NoWarn>
  </PropertyGroup>
  
  <!-- Package references -->
</Project>
```

### Your Task (Day 1):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Install Swagger package
dotnet add package Swashbuckle.AspNetCore

# Update csproj for XML documentation
# Add <GenerateDocumentationFile>true</GenerateDocumentationFile>

# Build and run
dotnet build
dotnet run

# Open browser to http://localhost:5000/swagger
```

---

# Day 2: Request/Response Examples & Schemas

## 🧒 Explain Like I'm 5

When you teach someone a new game, you don't just read the rules — you **show them**!
- "Look, I put THIS card HERE"
- "See, the result is THIS"

**Examples** in API documentation are the same — they show real data so developers know exactly what to send and what to expect back!

## 🔧 Engineer Language

OpenAPI examples provide concrete demonstrations of request payloads and expected responses. This helps API consumers understand data shapes without reading source code.

> 📖 **Microsoft Docs**: *"Use the WithOpenApi extension method to provide metadata that describes the endpoint in the OpenAPI document."*
>
> — [OpenAPI in Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/openapi)

### Feature Endpoint Documentation:

Update feature endpoints to include rich documentation. Here's how to document the `CreateIngredient` endpoint:

Create `src/Nastart.Api/Features/Ingredients/IngredientsEndpoints.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Ingredients;

/// <summary>
/// Endpoint mappings for Ingredient features.
/// </summary>
/// <remarks>
/// Following Minimal API best practices.
/// See: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis
/// </remarks>
public static class IngredientsEndpoints
{
    public static void MapIngredientsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ingredients")
            .WithTags("Ingredients")
            .WithOpenApi();
        
        // ── CREATE INGREDIENT ──
        group.MapPost("/", CreateIngredient)
            .WithName("CreateIngredient")
            .WithSummary("Create a new ingredient")
            .WithDescription("""
                Creates a new ingredient in the user's inventory.
                The ingredient will be tracked for price changes and stock levels.
                """)
            .Produces<IngredientResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .WithOpenApi(operation =>
            {
                operation.RequestBody.Description = "Ingredient details to create";
                return operation;
            });
        
        // ── GET INGREDIENT ──
        group.MapGet("/{id:guid}", GetIngredient)
            .WithName("GetIngredient")
            .WithSummary("Get ingredient by ID")
            .WithDescription("Retrieves a single ingredient with current price and stock information.")
            .Produces<IngredientResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
        
        // ── GET ALL INGREDIENTS ──
        group.MapGet("/", GetIngredients)
            .WithName("GetIngredients")
            .WithSummary("List all ingredients")
            .WithDescription("""
                Returns a list of all ingredients for the user.
                Supports filtering by category and low stock status.
                """)
            .Produces<IReadOnlyList<IngredientResponse>>(StatusCodes.Status200OK);
        
        // ── UPDATE PRICE ──
        group.MapPatch("/{id:guid}/price", UpdatePrice)
            .WithName("UpdateIngredientPrice")
            .WithSummary("Update ingredient price")
            .WithDescription("""
                Updates the current price of an ingredient.
                This will trigger price change detection and may create alerts.
                """)
            .Produces<IngredientResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);
        
        // ── GET LOW STOCK ──
        group.MapGet("/low-stock", GetLowStock)
            .WithName("GetLowStockIngredients")
            .WithSummary("Get ingredients with low stock")
            .WithDescription("Returns ingredients where current stock is below minimum stock threshold.")
            .Produces<IReadOnlyList<IngredientResponse>>(StatusCodes.Status200OK);
    }
    
    /// <summary>
    /// Creates a new ingredient.
    /// </summary>
    /// <param name="command">Ingredient creation details</param>
    /// <param name="mediator">MediatR instance</param>
    /// <returns>Created ingredient</returns>
    private static async Task<Results<Created<IngredientResponse>, BadRequest<Error>, Conflict<Error>>> 
        CreateIngredient(
            [FromBody] CreateIngredientCommand command,
            [FromServices] IMediator mediator)
    {
        var result = await mediator.Send(command);
        
        if (!result.IsSuccess)
        {
            return result.Error!.Type switch
            {
                "Conflict" => TypedResults.Conflict(result.Error),
                _ => TypedResults.BadRequest(result.Error)
            };
        }
        
        return TypedResults.Created(
            $"/api/ingredients/{result.Value!.Id}", 
            result.Value);
    }
    
    private static async Task<Results<Ok<IngredientResponse>, NotFound<Error>>> 
        GetIngredient(
            Guid id,
            [FromQuery] Guid userId,
            [FromServices] IMediator mediator)
    {
        var result = await mediator.Send(new GetIngredientQuery(id, userId));
        
        if (!result.IsSuccess)
            return TypedResults.NotFound(result.Error);
        
        return TypedResults.Ok(result.Value);
    }
    
    private static async Task<Ok<IReadOnlyList<IngredientResponse>>> 
        GetIngredients(
            [FromQuery] Guid userId,
            [FromQuery] string? searchTerm,
            [FromQuery] Guid? categoryId,
            [FromQuery] bool? lowStockOnly,
            [FromServices] IMediator mediator)
    {
        var result = await mediator.Send(new GetIngredientsQuery(
            userId, searchTerm, categoryId, lowStockOnly));
        
        return TypedResults.Ok(result.Value ?? []);
    }
    
    private static async Task<Results<Ok<IngredientResponse>, NotFound<Error>>> 
        UpdatePrice(
            Guid id,
            [FromBody] UpdatePriceRequest request,
            [FromServices] IMediator mediator)
    {
        var result = await mediator.Send(new UpdateIngredientPriceCommand(
            id, request.UserId, request.NewPrice));
        
        if (!result.IsSuccess)
            return TypedResults.NotFound(result.Error);
        
        return TypedResults.Ok(result.Value);
    }
    
    private static async Task<Ok<IReadOnlyList<IngredientResponse>>> 
        GetLowStock(
            [FromQuery] Guid userId,
            [FromServices] IMediator mediator)
    {
        var result = await mediator.Send(new GetIngredientsQuery(
            userId, LowStockOnly: true));
        
        return TypedResults.Ok(result.Value ?? []);
    }
}

/// <summary>
/// Request body for updating ingredient price.
/// </summary>
/// <param name="UserId">User ID who owns the ingredient</param>
/// <param name="NewPrice">New price in IDR</param>
/// <example>
/// {
///   "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
///   "newPrice": 25000
/// }
/// </example>
public sealed record UpdatePriceRequest(Guid UserId, decimal NewPrice);
```

### Add XML Comments to Commands/Queries:

Update feature commands with rich XML documentation:

```csharp
// In CreateIngredient.cs

/// <summary>
/// Command to create a new ingredient.
/// </summary>
/// <param name="UserId">The ID of the user creating the ingredient</param>
/// <param name="Name">Name of the ingredient (e.g., "Tepung Terigu")</param>
/// <param name="Unit">Unit of measurement (e.g., "kg", "pcs", "liter")</param>
/// <param name="CurrentPrice">Current price per unit in IDR (null if not yet priced)</param>
/// <param name="CurrentStock">Current stock quantity</param>
/// <param name="MinStock">Minimum stock threshold for alerts</param>
/// <param name="CategoryId">Optional category ID for grouping</param>
/// <example>
/// {
///   "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
///   "name": "Tepung Terigu",
///   "unit": "kg",
///   "currentPrice": 18000,  // optional — omit for unpriced ingredients
///   "currentStock": 10,
///   "minStock": 2,
///   "categoryId": "a0000001-0000-0000-0000-000000000001"
/// }
/// </example>
public sealed record CreateIngredientCommand(
    Guid UserId,
    string Name,
    string Unit,
    decimal? CurrentPrice,
    decimal CurrentStock,
    decimal MinStock,
    Guid? CategoryId = null
) : IRequest<Result<IngredientResponse>>;
```

### Response Examples with SwaggerExample Attribute:

Create `src/Nastart.Api/Shared/OpenApi/SwaggerExamples.cs`:

```csharp
using Swashbuckle.AspNetCore.Filters;

namespace Nastart.Api.Shared.OpenApi;

/// <summary>
/// Example provider for Ingredient responses.
/// </summary>
public class IngredientResponseExample : IExamplesProvider<IngredientResponse>
{
    public IngredientResponse GetExamples() => new(
        Id: Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
        Name: "Tepung Terigu",
        Unit: "kg",
        CurrentPrice: 18000m,
        CurrentStock: 10.5m,
        MinStock: 2m,
        IsLowStock: false,
        HasPrice: true,
        CategoryId: Guid.Parse("a0000001-0000-0000-0000-000000000001"),
        CategoryName: "Bahan Dasar"
    );
}

/// <summary>
/// Example provider for Purchase responses.
/// </summary>
public class PurchaseResponseExample : IExamplesProvider<PurchaseResponse>
{
    public PurchaseResponse GetExamples() => new(
        Id: Guid.Parse("4fa85f64-5717-4562-b3fc-2c963f66afa7"),
        ShopName: "Superindo Kemang",
        PurchaseDate: new DateTime(2026, 2, 5),
        TotalAmount: 105000m,
        ItemCount: 5,
        Status: PurchaseStatus.Confirmed,
        Items: new List<PurchaseItemResponse>
        {
            new(
                Id: Guid.NewGuid(),
                IngredientId: Guid.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                IngredientName: "Tepung Terigu",
                Quantity: 2m,
                UnitPrice: 18000m,
                TotalPrice: 36000m
            )
        },
        CreatedAt: DateTime.UtcNow
    );
}
```

### Register Examples in Program.cs:

```csharp
builder.Services.AddSwaggerGen(options =>
{
    // ... existing configuration
    
    // Add example filters
    options.ExampleFilters();
});

// Register example providers
builder.Services.AddSwaggerExamplesFromAssemblyOf<Program>();
```

### Your Task (Day 2):

1. Create `IngredientsEndpoints.cs` with full documentation
2. Add XML comments to all command/query records
3. Create example providers in `Shared/OpenApi/`
4. Register examples in `Program.cs`
5. Verify documentation renders in Swagger UI

---

# Day 3: Health Check Endpoints

## 🧒 Explain Like I'm 5

When you wake up, mom might ask: "How are you feeling today?" 
- "I feel great!" ✅
- "My tummy hurts..." ❌

**Health checks** are the same for your app — they answer "Are you working okay?"
- "Database connection? ✅ Working!"
- "OCR service? ✅ Connected!"
- "Memory? ❌ Too much used — need help!"

This helps us know if something is broken before users complain!

## 🔧 Engineer Language

**Health checks** are endpoints that report the status of application components. ASP.NET Core has built-in health check infrastructure with support for database, external services, and custom checks.

> 📖 **Microsoft Docs**: *"Health checks are exposed as middleware. They provide a way to expose the health of an application through an HTTP endpoint."*
>
> — [Health checks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)

### Install Health Check Packages:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Database health check
dotnet add package AspNetCore.HealthChecks.NpgSql

# URI health check (for OCR service)
dotnet add package AspNetCore.HealthChecks.Uris

# Health check UI (optional)
dotnet add package AspNetCore.HealthChecks.UI
dotnet add package AspNetCore.HealthChecks.UI.InMemory.Storage
```

### Configure Health Checks:

Create `src/Nastart.Api/Shared/HealthChecks/CustomHealthChecks.cs`:

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nastart.Api.Shared.Services.OcrService;

namespace Nastart.Api.Shared.HealthChecks;

/// <summary>
/// Custom health check for OCR service.
/// </summary>
/// <remarks>
/// Verifies the PaddleOCR microservice is reachable.
/// See: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks
/// </remarks>
public class OcrServiceHealthCheck : IHealthCheck
{
    private readonly IPaddleOcrService _ocrService;
    private readonly ILogger<OcrServiceHealthCheck> _logger;

    public OcrServiceHealthCheck(
        IPaddleOcrService ocrService,
        ILogger<OcrServiceHealthCheck> logger)
    {
        _ocrService = ocrService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _ocrService.IsHealthyAsync(cancellationToken);
            
            if (isHealthy)
            {
                return HealthCheckResult.Healthy(
                    "OCR service is responding normally.");
            }
            
            return HealthCheckResult.Degraded(
                "OCR service is not responding correctly.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR health check failed");
            
            return HealthCheckResult.Unhealthy(
                "OCR service is unavailable.",
                exception: ex);
        }
    }
}

/// <summary>
/// Health check for database connectivity and query performance.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly NastartDbContext _db;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(
        NastartDbContext db,
        ILogger<DatabaseHealthCheck> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            // Simple query to test connection
            _ = await _db.Database.CanConnectAsync(cancellationToken);
            
            sw.Stop();
            
            var data = new Dictionary<string, object>
            {
                ["ResponseTimeMs"] = sw.ElapsedMilliseconds
            };
            
            if (sw.ElapsedMilliseconds > 1000)
            {
                return HealthCheckResult.Degraded(
                    $"Database responding slowly ({sw.ElapsedMilliseconds}ms).",
                    data: data);
            }
            
            return HealthCheckResult.Healthy(
                $"Database connection OK ({sw.ElapsedMilliseconds}ms).",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            
            return HealthCheckResult.Unhealthy(
                "Database connection failed.",
                exception: ex);
        }
    }
}
```

### Register Health Checks in Program.cs:

```csharp
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using Nastart.Api.Shared.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// ══════════════════════════════════════════════════════════════
// HEALTH CHECKS CONFIGURATION
// ══════════════════════════════════════════════════════════════

builder.Services.AddHealthChecks()
    // PostgreSQL database
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db", "sql", "postgresql" })
    
    // OCR Service
    .AddCheck<OcrServiceHealthCheck>(
        name: "ocr-service",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "external", "ocr" })
    
    // Custom database check with timing
    .AddCheck<DatabaseHealthCheck>(
        name: "database-performance",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "db", "performance" });

var app = builder.Build();

// ══════════════════════════════════════════════════════════════
// HEALTH CHECK ENDPOINTS
// ══════════════════════════════════════════════════════════════

// Basic health check (for load balancers)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false, // Don't run any checks, just return 200
    ResponseWriter = (context, report) =>
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(
            JsonSerializer.Serialize(new { status = "healthy" }));
    }
});

// Liveness probe (is the app running?)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = (context, report) =>
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(
            JsonSerializer.Serialize(new 
            { 
                status = "alive",
                timestamp = DateTime.UtcNow 
            }));
    }
});

// Readiness probe (is the app ready to serve requests?)
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db"),
    ResponseWriter = WriteHealthCheckResponse
});

// Detailed health check (for debugging)
app.MapHealthChecks("/health/detailed", new HealthCheckOptions
{
    ResponseWriter = WriteHealthCheckResponse
}).RequireAuthorization(); // Only for authenticated users

// Health check response writer
static Task WriteHealthCheckResponse(
    HttpContext context, 
    HealthReport report)
{
    context.Response.ContentType = "application/json";
    
    var response = new
    {
        status = report.Status.ToString(),
        totalDuration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            duration = e.Value.Duration.TotalMilliseconds,
            data = e.Value.Data,
            exception = e.Value.Exception?.Message
        })
    };
    
    var options = new JsonSerializerOptions 
    { 
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    return context.Response.WriteAsJsonAsync(response, options);
}
```

### Add Health Checks to OpenAPI:

```csharp
// In endpoint configuration
app.MapHealthChecks("/health/ready", new HealthCheckOptions { /* ... */ })
    .WithTags("Health")
    .WithName("ReadinessProbe")
    .WithSummary("Checks if the application is ready to serve requests")
    .Produces<object>(StatusCodes.Status200OK)
    .Produces<object>(StatusCodes.Status503ServiceUnavailable);
```

### Your Task (Day 3):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Install health check packages
dotnet add package AspNetCore.HealthChecks.NpgSql
dotnet add package AspNetCore.HealthChecks.Uris

# Create health check folder
mkdir Shared\HealthChecks
New-Item Shared\HealthChecks\CustomHealthChecks.cs

# Build and run
dotnet build
dotnet run

# Test endpoints
# GET http://localhost:5000/health
# GET http://localhost:5000/health/live
# GET http://localhost:5000/health/ready
```

---

# Day 4: CORS Configuration for Frontend

## 🧒 Explain Like I'm 5

Imagine your house has a rule: "Only friends from OUR neighborhood can come in!"

But your cousin lives in a different neighborhood and wants to visit. You need to say:
- "Okay, cousins from Neighborhood X are allowed too!"

**CORS (Cross-Origin Resource Sharing)** is the same — it tells your API:
- "Requests from THIS website are allowed"
- "Requests from THAT website are blocked"

This keeps your API safe from bad websites trying to steal data!

## 🔧 Engineer Language

**CORS** controls which domains can make cross-origin requests to your API. Without proper CORS configuration, browsers will block requests from frontend applications hosted on different domains.

> 📖 **Microsoft Docs**: *"CORS is a W3C standard that allows a server to relax the same-origin policy. It lets servers specify who can access their resources."*
>
> — [Enable Cross-Origin Requests (CORS) in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/cors)

### CORS Configuration:

Create `src/Nastart.Api/Shared/Configuration/CorsConfiguration.cs`:

```csharp
namespace Nastart.Api.Shared.Configuration;

/// <summary>
/// CORS configuration options.
/// </summary>
public class CorsOptions
{
    public const string SectionName = "Cors";
    
    /// <summary>
    /// Allowed origins for CORS requests.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
    
    /// <summary>
    /// Whether to allow credentials (cookies, auth headers).
    /// </summary>
    public bool AllowCredentials { get; set; } = false;
    
    /// <summary>
    /// Allowed HTTP methods.
    /// </summary>
    public string[] AllowedMethods { get; set; } = ["GET", "POST", "PUT", "PATCH", "DELETE"];
    
    /// <summary>
    /// Allowed HTTP headers.
    /// </summary>
    public string[] AllowedHeaders { get; set; } = ["Content-Type", "Authorization"];
    
    /// <summary>
    /// Headers to expose in responses.
    /// </summary>
    public string[] ExposedHeaders { get; set; } = ["X-Pagination"];
    
    /// <summary>
    /// Max age for preflight cache (seconds).
    /// </summary>
    public int MaxAge { get; set; } = 86400; // 24 hours
}

/// <summary>
/// Extension methods for CORS configuration.
/// </summary>
public static class CorsConfigurationExtensions
{
    public const string CorsPolicyName = "NastartCorsPolicy";
    
    /// <summary>
    /// Adds CORS services with configuration from appsettings.
    /// </summary>
    public static IServiceCollection AddNastartCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var corsOptions = configuration
            .GetSection(CorsOptions.SectionName)
            .Get<CorsOptions>() ?? new CorsOptions();
        
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, builder =>
            {
                // Configure origins
                if (corsOptions.AllowedOrigins.Length == 0 ||
                    corsOptions.AllowedOrigins.Contains("*"))
                {
                    builder.AllowAnyOrigin();
                }
                else
                {
                    builder.WithOrigins(corsOptions.AllowedOrigins);
                }
                
                // Configure methods
                if (corsOptions.AllowedMethods.Contains("*"))
                {
                    builder.AllowAnyMethod();
                }
                else
                {
                    builder.WithMethods(corsOptions.AllowedMethods);
                }
                
                // Configure headers
                if (corsOptions.AllowedHeaders.Contains("*"))
                {
                    builder.AllowAnyHeader();
                }
                else
                {
                    builder.WithHeaders(corsOptions.AllowedHeaders);
                }
                
                // Exposed headers
                if (corsOptions.ExposedHeaders.Length > 0)
                {
                    builder.WithExposedHeaders(corsOptions.ExposedHeaders);
                }
                
                // Credentials
                if (corsOptions.AllowCredentials)
                {
                    builder.AllowCredentials();
                }
                
                // Preflight cache
                builder.SetPreflightMaxAge(
                    TimeSpan.FromSeconds(corsOptions.MaxAge));
            });
            
            // Development policy (more permissive)
            options.AddPolicy("Development", builder =>
            {
                builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });
        
        return services;
    }
}
```

### Add to appsettings.json:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:5173",
      "https://nastart.app"
    ],
    "AllowCredentials": true,
    "AllowedMethods": ["GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"],
    "AllowedHeaders": ["Content-Type", "Authorization", "X-Request-Id"],
    "ExposedHeaders": ["X-Pagination", "X-Request-Id"],
    "MaxAge": 86400
  }
}
```

### Environment-Specific Configuration:

Create `appsettings.Development.json`:

```json
{
  "Cors": {
    "AllowedOrigins": ["*"],
    "AllowCredentials": false
  }
}
```

Create `appsettings.Production.json`:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://nastart.app",
      "https://www.nastart.app"
    ],
    "AllowCredentials": true
  }
}
```

### Register in Program.cs:

```csharp
using Nastart.Api.Shared.Configuration;

var builder = WebApplication.CreateBuilder(args);

// ══════════════════════════════════════════════════════════════
// CORS CONFIGURATION
// ══════════════════════════════════════════════════════════════

builder.Services.AddNastartCors(builder.Configuration);

var app = builder.Build();

// Use CORS before routing
if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
}
else
{
    app.UseCors(CorsConfigurationExtensions.CorsPolicyName);
}

// ... rest of middleware
```

### Your Task (Day 4):

1. Create `Shared/Configuration/CorsConfiguration.cs`
2. Add CORS settings to `appsettings.json`
3. Create environment-specific settings
4. Register CORS in `Program.cs`
5. Test with browser DevTools Network tab

---

# Day 5: Rate Limiting for Bot Endpoints

## 🧒 Explain Like I'm 5

Imagine you're giving out candy 🍬 at a party. If one kid keeps coming back again and again and again, other kids don't get any!

So you make a rule: "Each kid can get candy only 3 times per minute!"

**Rate limiting** is the same — it makes sure one user (or bot) doesn't hog all the server resources by making too many requests!

## 🔧 Engineer Language

**Rate limiting** prevents abuse by restricting the number of requests a client can make within a time window. ASP.NET Core 7+ includes built-in rate limiting middleware.

> 📖 **Microsoft Docs**: *"Rate limiting middleware provides rate limiting services. It can be used to protect endpoints from being overwhelmed with requests."*
>
> — [Rate limiting middleware in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)

### Configure Rate Limiting:

Create `src/Nastart.Api/Shared/Configuration/RateLimitConfiguration.cs`:

```csharp
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Nastart.Api.Shared.Configuration;

/// <summary>
/// Rate limiting configuration for API endpoints.
/// </summary>
/// <remarks>
/// Uses token bucket algorithm for flexible rate limiting.
/// See: https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit
/// </remarks>
public static class RateLimitConfiguration
{
    public const string DefaultPolicy = "default";
    public const string BotPolicy = "bot";
    public const string OcrPolicy = "ocr";
    public const string AuthenticatedPolicy = "authenticated";
    
    /// <summary>
    /// Adds rate limiting services.
    /// </summary>
    public static IServiceCollection AddNastartRateLimiting(
        this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Default policy: 100 requests per minute
            options.AddFixedWindowLimiter(DefaultPolicy, config =>
            {
                config.PermitLimit = 100;
                config.Window = TimeSpan.FromMinutes(1);
                config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                config.QueueLimit = 10;
            });
            
            // Bot endpoints: stricter limits (Telegram webhook)
            options.AddTokenBucketLimiter(BotPolicy, config =>
            {
                config.TokenLimit = 30;
                config.TokensPerPeriod = 10;
                config.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
                config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                config.QueueLimit = 5;
            });
            
            // OCR endpoints: limited due to resource intensity
            options.AddConcurrencyLimiter(OcrPolicy, config =>
            {
                config.PermitLimit = 5; // Max 5 concurrent OCR requests
                config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                config.QueueLimit = 10;
            });
            
            // Authenticated users: higher limits
            options.AddSlidingWindowLimiter(AuthenticatedPolicy, config =>
            {
                config.PermitLimit = 200;
                config.Window = TimeSpan.FromMinutes(1);
                config.SegmentsPerWindow = 4;
                config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                config.QueueLimit = 20;
            });
            
            // Global rate limit exceeded handler
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";
                
                var retryAfter = context.Lease.TryGetMetadata(
                    MetadataName.RetryAfter, out var retryAfterValue)
                    ? retryAfterValue.TotalSeconds
                    : 60;
                
                context.HttpContext.Response.Headers.RetryAfter = 
                    ((int)retryAfter).ToString();
                
                var response = new
                {
                    type = "https://httpstatuses.io/429",
                    title = "Too Many Requests",
                    status = 429,
                    detail = "Rate limit exceeded. Please try again later.",
                    retryAfterSeconds = (int)retryAfter
                };
                
                await context.HttpContext.Response.WriteAsJsonAsync(
                    response, cancellationToken);
            };
            
            // Partition by IP address for anonymous requests
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context =>
                {
                    // Use user ID if authenticated, otherwise IP address
                    var userId = context.User?.Identity?.Name;
                    var partitionKey = userId ?? 
                        context.Connection.RemoteIpAddress?.ToString() ?? 
                        "unknown";
                    
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 1000,
                            Window = TimeSpan.FromHours(1)
                        });
                });
        });
        
        return services;
    }
}
```

### Apply Rate Limiting to Endpoints:

```csharp
// In Program.cs
using Nastart.Api.Shared.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add rate limiting
builder.Services.AddNastartRateLimiting();

var app = builder.Build();

// Use rate limiter middleware
app.UseRateLimiter();

// Apply to endpoint groups
app.MapGroup("/api/ingredients")
    .RequireRateLimiting(RateLimitConfiguration.DefaultPolicy);

app.MapGroup("/api/bot")
    .RequireRateLimiting(RateLimitConfiguration.BotPolicy);

app.MapGroup("/api/purchases/scan")
    .RequireRateLimiting(RateLimitConfiguration.OcrPolicy);
```

### Rate Limit Headers:

Create middleware to add rate limit headers to responses:

```csharp
// Shared/Middleware/RateLimitHeadersMiddleware.cs

namespace Nastart.Api.Shared.Middleware;

/// <summary>
/// Middleware to add rate limit headers to responses.
/// </summary>
public class RateLimitHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public RateLimitHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);
        
        // Add rate limit headers if available
        if (context.Response.Headers.ContainsKey("X-RateLimit-Limit"))
            return;
        
        // These would be set by the rate limiter
        // Adding placeholder headers for documentation
        context.Response.Headers.Append("X-RateLimit-Policy", "default");
    }
}
```

### Your Task (Day 5):

1. Create `Shared/Configuration/RateLimitConfiguration.cs`
2. Register rate limiting in `Program.cs`
3. Apply policies to endpoint groups
4. Test with rapid requests using `curl` or Postman

---

# Day 6: Global Error Handling Middleware

## 🧒 Explain Like I'm 5

When something goes wrong, you don't want to cry in front of everyone! Instead, you quietly tell Mom about it.

**Error handling middleware** does the same — when your app has a problem:
1. It catches the error (before users see scary messages)
2. It writes down what happened (logging)
3. It shows a nice, friendly message to users

## 🔧 Engineer Language

**Global error handling** catches unhandled exceptions and converts them to proper HTTP responses. This ensures consistent error responses and prevents sensitive information from leaking to clients.

> 📖 **Microsoft Docs**: *"Use middleware to handle exceptions globally. The middleware catches exceptions and converts them to responses."*
>
> — [Handle errors in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling)

### Exception Handling Middleware:

Create `src/Nastart.Api/Shared/Middleware/GlobalExceptionHandlerMiddleware.cs`:

```csharp
using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Nastart.Api.Shared.Middleware;

/// <summary>
/// Global exception handling middleware.
/// </summary>
/// <remarks>
/// Catches all unhandled exceptions and returns RFC 7807 Problem Details responses.
/// See: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling
/// </remarks>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        
        _logger.LogError(
            exception,
            "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}",
            traceId,
            context.Request.Path);
        
        var problemDetails = CreateProblemDetails(exception, traceId);
        
        context.Response.StatusCode = problemDetails.Status ?? 500;
        context.Response.ContentType = "application/problem+json";
        
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        await context.Response.WriteAsJsonAsync(problemDetails, options);
    }

    private ProblemDetails CreateProblemDetails(Exception exception, string traceId)
    {
        return exception switch
        {
            ValidationException validationEx => new ProblemDetails
            {
                Type = "https://httpstatuses.io/400",
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Detail = "One or more validation errors occurred.",
                Extensions =
                {
                    ["traceId"] = traceId,
                    ["errors"] = validationEx.Errors.Select(e => new
                    {
                        property = e.PropertyName,
                        message = e.ErrorMessage,
                        code = e.ErrorCode
                    })
                }
            },
            
            ArgumentNullException argNullEx => new ProblemDetails
            {
                Type = "https://httpstatuses.io/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = $"Required parameter '{argNullEx.ParamName}' is missing.",
                Extensions = { ["traceId"] = traceId }
            },
            
            ArgumentException argEx => new ProblemDetails
            {
                Type = "https://httpstatuses.io/400",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = argEx.Message,
                Extensions = { ["traceId"] = traceId }
            },
            
            UnauthorizedAccessException => new ProblemDetails
            {
                Type = "https://httpstatuses.io/401",
                Title = "Unauthorized",
                Status = StatusCodes.Status401Unauthorized,
                Detail = "You are not authorized to access this resource.",
                Extensions = { ["traceId"] = traceId }
            },
            
            KeyNotFoundException => new ProblemDetails
            {
                Type = "https://httpstatuses.io/404",
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = "The requested resource was not found.",
                Extensions = { ["traceId"] = traceId }
            },
            
            OperationCanceledException => new ProblemDetails
            {
                Type = "https://httpstatuses.io/499",
                Title = "Client Closed Request",
                Status = 499,
                Detail = "The request was cancelled by the client.",
                Extensions = { ["traceId"] = traceId }
            },
            
            TimeoutException => new ProblemDetails
            {
                Type = "https://httpstatuses.io/408",
                Title = "Request Timeout",
                Status = StatusCodes.Status408RequestTimeout,
                Detail = "The request took too long to process.",
                Extensions = { ["traceId"] = traceId }
            },
            
            HttpRequestException httpEx => new ProblemDetails
            {
                Type = "https://httpstatuses.io/502",
                Title = "Bad Gateway",
                Status = StatusCodes.Status502BadGateway,
                Detail = "An external service is unavailable.",
                Extensions = { ["traceId"] = traceId }
            },
            
            _ => new ProblemDetails
            {
                Type = "https://httpstatuses.io/500",
                Title = "Internal Server Error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = _environment.IsDevelopment() 
                    ? exception.Message 
                    : "An unexpected error occurred.",
                Extensions = 
                {
                    ["traceId"] = traceId,
                    ["exception"] = _environment.IsDevelopment() 
                        ? exception.ToString() 
                        : null
                }
            }
        };
    }
}

/// <summary>
/// Extension method to register the middleware.
/// </summary>
public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
```

### Request Logging Middleware:

Create `src/Nastart.Api/Shared/Middleware/RequestLoggingMiddleware.cs`:

```csharp
using System.Diagnostics;

namespace Nastart.Api.Shared.Middleware;

/// <summary>
/// Middleware to log requests and responses.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N")[..8];
        
        // Add request ID to response headers
        context.Response.Headers.Append("X-Request-Id", requestId);
        
        // Log request
        _logger.LogInformation(
            "[{RequestId}] {Method} {Path}{QueryString} - Started",
            requestId,
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString);
        
        await _next(context);
        
        sw.Stop();
        
        // Log response
        var level = context.Response.StatusCode >= 500 
            ? LogLevel.Error 
            : context.Response.StatusCode >= 400 
                ? LogLevel.Warning 
                : LogLevel.Information;
        
        _logger.Log(
            level,
            "[{RequestId}] {Method} {Path} - {StatusCode} ({Duration}ms)",
            requestId,
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            sw.ElapsedMilliseconds);
    }
}

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestLoggingMiddleware>();
    }
}
```

### Register Middleware in Program.cs:

```csharp
using Nastart.Api.Shared.Middleware;

var app = builder.Build();

// ══════════════════════════════════════════════════════════════
// MIDDLEWARE PIPELINE (ORDER MATTERS!)
// ══════════════════════════════════════════════════════════════

// 1. Global exception handler (outermost)
app.UseGlobalExceptionHandler();

// 2. Request logging
app.UseRequestLogging();

// 3. CORS
app.UseCors();

// 4. Rate limiting
app.UseRateLimiter();

// 5. Authentication (if any)
// app.UseAuthentication();
// app.UseAuthorization();

// 6. Swagger (development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 7. Endpoints
app.MapEndpointsFromFeatures();

app.Run();
```

### Your Task (Day 6):

1. Create `Shared/Middleware/GlobalExceptionHandlerMiddleware.cs`
2. Create `Shared/Middleware/RequestLoggingMiddleware.cs`
3. Register middleware in correct order in `Program.cs`
4. Test by throwing exceptions in handlers
5. Verify Problem Details format in responses

---

# Day 7: API Testing & Documentation Review

## 🧒 Explain Like I'm 5

Before a race, athletes practice and check everything:
- "Are my shoes tied? ✅"
- "Did I stretch? ✅"
- "Is my water bottle ready? ✅"

Before launching your API, you test EVERYTHING:
- "Does the documentation look good? ✅"
- "Do all endpoints work? ✅"
- "Are errors handled nicely? ✅"

## 🔧 Engineer Language

**API testing** validates that your endpoints work correctly, return proper status codes, and handle edge cases. Combined with documentation review, this ensures your API is production-ready.

> 📖 **Microsoft Docs**: *"Integration tests ensure that an app's components function correctly at a level that includes infrastructure like the database and network."*
>
> — [Integration tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)

### API Integration Tests:

Create `tests/Nastart.Api.Tests/Integration/ApiEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Tests.Integration;

/// <summary>
/// Integration tests for API endpoints.
/// </summary>
/// <remarks>
/// Uses WebApplicationFactory for in-memory testing.
/// See: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests
/// </remarks>
public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Configure test services if needed
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("healthy");
    }

    [Fact]
    public async Task HealthReady_ReturnsStatus()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");
        
        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, 
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task SwaggerJson_IsAvailable()
    {
        // Act
        var response = await _client.GetAsync("/api-docs/v1/swagger.json");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/json");
    }

    [Fact]
    public async Task CreateIngredient_WithValidData_ReturnsCreated()
    {
        // Arrange
        var command = new CreateIngredientCommand(
            UserId: Guid.NewGuid(),
            Name: "Test Ingredient",
            Unit: "kg",
            CurrentPrice: 10000m,
            CurrentStock: 5m,
            MinStock: 1m);
        
        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/ingredients", 
            command);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var result = await response.Content
            .ReadFromJsonAsync<IngredientResponse>();
        
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Ingredient");
    }

    [Fact]
    public async Task CreateIngredient_WithInvalidData_ReturnsBadRequest()
    {
        // Arrange
        var command = new CreateIngredientCommand(
            UserId: Guid.NewGuid(),
            Name: "", // Invalid: empty name
            Unit: "kg",
            CurrentPrice: -100m, // Invalid: negative price
            CurrentStock: 5m,
            MinStock: 1m);
        
        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/ingredients", 
            command);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetIngredient_NotFound_Returns404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        
        // Act
        var response = await _client.GetAsync(
            $"/api/ingredients/{nonExistentId}?userId={Guid.NewGuid()}");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cors_PreflightRequest_ReturnsHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/ingredients");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        
        // Act
        var response = await _client.SendAsync(request);
        
        // Assert
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
    }

    [Fact]
    public async Task RateLimiting_ExcessiveRequests_Returns429()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 150)
            .Select(_ => _client.GetAsync("/api/ingredients?userId=" + Guid.NewGuid()));
        
        // Act
        var responses = await Task.WhenAll(tasks);
        
        // Assert
        responses.Should().Contain(r => 
            r.StatusCode == HttpStatusCode.TooManyRequests);
    }
}
```

### Documentation Checklist:

Create a checklist for reviewing API documentation:

| Check | Status | Notes |
|-------|--------|-------|
| All endpoints documented in Swagger | ⬜ | |
| Request/response examples present | ⬜ | |
| Validation rules documented | ⬜ | |
| Error responses documented | ⬜ | |
| Authentication requirements noted | ⬜ | |
| Rate limit policies documented | ⬜ | |
| Health check endpoints listed | ⬜ | |
| API versioning clear | ⬜ | |

### Postman/REST Client Testing:

Create `tests/api/ingredients.http` for VS Code REST Client:

```http
### Variables
@baseUrl = http://localhost:5000
@userId = 3fa85f64-5717-4562-b3fc-2c963f66afa6

### Health Check
GET {{baseUrl}}/health

### Health Ready
GET {{baseUrl}}/health/ready

### Create Ingredient
POST {{baseUrl}}/api/ingredients
Content-Type: application/json

{
  "userId": "{{userId}}",
  "name": "Tepung Terigu",
  "unit": "kg",
  "currentPrice": 18000,  // optional — omit for unpriced ingredients
  "currentStock": 10,
  "minStock": 2,
  "categoryId": "a0000001-0000-0000-0000-000000000001"
}

### Get All Ingredients
GET {{baseUrl}}/api/ingredients?userId={{userId}}

### Get Single Ingredient
GET {{baseUrl}}/api/ingredients/{{ingredientId}}?userId={{userId}}

### Update Price
PATCH {{baseUrl}}/api/ingredients/{{ingredientId}}/price
Content-Type: application/json

{
  "userId": "{{userId}}",
  "newPrice": 20000
}

### Get Low Stock
GET {{baseUrl}}/api/ingredients/low-stock?userId={{userId}}
```

### Run All Tests:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend

# Run unit tests
dotnet test --filter "Category=Unit"

# Run integration tests
dotnet test --filter "Category=Integration"

# Run all tests with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Your Task (Day 7):

1. Create integration test file
2. Create `.http` files for manual testing
3. Run all tests: `dotnet test`
4. Review Swagger documentation at `/swagger`
5. Complete documentation checklist
6. Fix any issues found

---

# Resources

## Microsoft Official Documentation

| Topic | Link |
|-------|------|
| **OpenAPI** | [learn.microsoft.com/aspnet/core/fundamentals/openapi](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview) |
| **Minimal APIs OpenAPI** | [learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/openapi](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/openapi) |
| **Health Checks** | [learn.microsoft.com/aspnet/core/host-and-deploy/health-checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks) |
| **CORS** | [learn.microsoft.com/aspnet/core/security/cors](https://learn.microsoft.com/en-us/aspnet/core/security/cors) |
| **Rate Limiting** | [learn.microsoft.com/aspnet/core/performance/rate-limit](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit) |
| **Error Handling** | [learn.microsoft.com/aspnet/core/fundamentals/error-handling](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling) |
| **Integration Tests** | [learn.microsoft.com/aspnet/core/test/integration-tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) |
| **Problem Details RFC 7807** | [datatracker.ietf.org/doc/html/rfc7807](https://datatracker.ietf.org/doc/html/rfc7807) |

## External Resources

| Topic | Link |
|-------|------|
| **Swashbuckle** | [github.com/domaindrivendev/Swashbuckle.AspNetCore](https://github.com/domaindrivendev/Swashbuckle.AspNetCore) |
| **FluentValidation** | [docs.fluentvalidation.net](https://docs.fluentvalidation.net/) |
| **ASP.NET Health Checks** | [github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks) |

## Week 7 Checklist

- [ ] Installed Swashbuckle.AspNetCore
- [ ] Configured OpenAPI in Program.cs
- [ ] Enabled XML documentation generation
- [ ] Created IngredientsEndpoints.cs with documentation
- [ ] Added request/response examples
- [ ] Created custom health checks
- [ ] Configured health check endpoints
- [ ] Set up CORS configuration
- [ ] Created environment-specific CORS settings
- [ ] Implemented rate limiting policies
- [ ] Applied rate limits to endpoint groups
- [ ] Created GlobalExceptionHandlerMiddleware
- [ ] Created RequestLoggingMiddleware
- [ ] Configured middleware in correct order
- [ ] Created integration tests
- [ ] Created .http test files
- [ ] Reviewed Swagger documentation
- [ ] All builds pass: `dotnet build`
- [ ] All tests pass: `dotnet test`

---

## What's Next?

**Week 8: Bot Fundamentals** — Create your Telegram bot with BotFather, implement webhook endpoints, build command routing, and create your first bot commands (`/start`, `/help`).

---

*Nastart — Start smart, bake profitable*
