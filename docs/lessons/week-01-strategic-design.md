# Week 1: Strategic Design with Vertical Slice Architecture 🎯

> **Goal**: Before writing any code, we need to understand WHAT we're building, WHY, and HOW to organize our code around features — not technical layers.

---

## Table of Contents
1. [Day 1-2: Feature Discovery](#day-1-2-feature-discovery)
2. [Day 3: Understanding Vertical Slice Architecture](#day-3-understanding-vertical-slice-architecture)
3. [Day 4: Feature Glossary & Naming](#day-4-feature-glossary--naming)
4. [Day 5: GitHub Repository Setup](#day-5-github-repository-setup)
5. [Day 6: .NET Solution Structure — Feature-Based (slnx)](#day-6-net-solution-structure--feature-based-slnx)
6. [Day 7: Docker & PostgreSQL with Best Practices](#day-7-docker--postgresql-with-best-practices)
7. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1-2: Feature Discovery

## 🧒 Explain Like I'm 5

Imagine you're making a list of everything a baker named Ibu Sari wants to do with the app:

> "Ibu Sari wants to **scan her receipt**. She wants to **see her ingredient prices**. She wants to **create a recipe**. She wants to **know if she's making money**. She wants to **get alerts** when flour gets expensive!"

Each thing she wants to do is a **feature** — it's like a button that does something useful!

Instead of organizing our code by *how* it works (all the buttons together, all the screens together), we organize it by *what* it does (everything about scanning receipts in one place, everything about recipes in another).

## 🔧 Engineer Language

**Feature Discovery** is a technique to identify the capabilities your application needs from a user's perspective. Unlike traditional layered architecture where code is grouped by technical concerns (Controllers, Services, Repositories), **Vertical Slice Architecture** organizes code by *feature* or *use case*.

### Why Vertical Slices?

| Traditional Layers | Vertical Slices |
|-------------------|-----------------|
| Change one feature → touch 5+ folders | Change one feature → touch 1 folder |
| Hard to understand feature flow | Easy to read entire feature |
| Tight coupling between layers | Loose coupling between features |
| `Controllers/`, `Services/`, `Repositories/` | `Features/ScanReceipt/`, `Features/CreateRecipe/` |

### Core Concepts:

| Concept | Description | Example |
|---------|-------------|---------|
| 🎯 **Feature** | A single user capability | "Scan Receipt", "Create Recipe" |
| 📦 **Slice** | All code for one feature in one folder | Handler, Request, Response, Validator |
| 📨 **Request** | Input for a feature (Command or Query) | `ScanReceiptCommand`, `GetRecipesQuery` |
| 📤 **Response** | Output from a feature | `ScanReceiptResult`, `RecipeListDto` |
| 🔗 **Handler** | The logic that processes a request | `ScanReceiptHandler.cs` |

### How to Discover Features:

1. **User Story Mapping** (30 min)
   - List what users want to DO (verbs)
   - Group by capability, not by data type

2. **Identify Commands vs Queries** (20 min)
   - Commands: Change state (Create, Update, Delete)
   - Queries: Read state (Get, List, Search)

3. **Name Your Slices** (20 min)
   - Use verb + noun: `ScanReceipt`, `CreateRecipe`, `GetPriceHistory`
   - These become folder names!

4. **Map Dependencies** (20 min)
   - What data does each feature need?
   - What other features might it trigger?

### Nastart Feature Map:

```
Features/
├── Users/
│   ├── RegisterUser/          ← Command: Create account (Telegram onboarding)
│   ├── GetUser/               ← Query: Get user profile
│   └── UpdateUserSettings/    ← Command: Update min margin, business type
│
├── Receipts/
│   ├── ScanReceipt/           ← Command: Upload & OCR receipt
│   ├── GetPurchaseHistory/    ← Query: List past purchases
│   └── MatchIngredients/      ← Command: Match OCR text to ingredients
│
├── Ingredients/
│   ├── CreateIngredient/      ← Command: Add new ingredient
│   ├── GetIngredient/         ← Query: Get ingredient details
│   ├── GetAllIngredients/     ← Query: List all ingredients
│   ├── UpdatePrice/           ← Command: Update ingredient price
│   └── GetPriceHistory/       ← Query: Price trends over time
│
├── Recipes/
│   ├── CreateRecipe/          ← Command: Create new recipe (with yield/batch)
│   ├── GetRecipe/             ← Query: Get recipe with cost-per-unit
│   ├── GetAllRecipes/         ← Query: List all recipes
│   ├── AddIngredientToRecipe/ ← Command: Add ingredient with quantity
│   └── CalculateCost/         ← Query: Get real-time cost (batch + per-unit)
│
├── Alerts/
│   ├── GetAlerts/             ← Query: Get pending alerts
│   ├── DismissAlert/          ← Command: Mark alert as read
│   └── CreatePriceSpikeAlert/ ← Internal: Auto-created on price change
│
└── Reports/
    ├── GetDashboard/          ← Query: Summary stats
    └── GetProfitReport/       ← Query: Profit analysis
```

### Feature Slice Structure:

Each feature folder is self-contained:

```
Features/
└── ScanReceipt/
    ├── ScanReceiptCommand.cs      ← The request (input)
    ├── ScanReceiptHandler.cs      ← The logic
    ├── ScanReceiptResult.cs       ← The response (output)
    ├── ScanReceiptValidator.cs    ← Input validation (optional)
    └── ScanReceiptEndpoint.cs     ← Minimal API endpoint mapping
```

### Your Task (Day 1-2):

1. Get paper/whiteboard or use Miro/FigJam
2. List ALL things a user wants to DO:
   - Register account
   - Link phone to Telegram
   - Upload receipt photo
   - View scanned items
   - Confirm/edit ingredient matches
   - Add new ingredient
   - View ingredient price history
   - Create a recipe
   - Add ingredients to recipe
   - View recipe cost breakdown
   - Set minimum margin
   - Receive price spike alert
   - View dashboard

3. Group by capability area (Receipts, Ingredients, Recipes, Alerts, Reports)
4. Mark each as Command (changes data) or Query (reads data)
5. Give each a verb+noun name: `ScanReceipt`, `CreateRecipe`, etc.

---

# Day 3: Understanding Vertical Slice Architecture

## 🧒 Explain Like I'm 5

Imagine you have a toy box with TWO ways to organize:

**Old Way (Layers):**
- One drawer for ALL wheels (car wheels, train wheels, bike wheels)
- One drawer for ALL bodies (car bodies, train bodies, bike bodies)
- To play with a car, you open 3 different drawers!

**New Way (Slices):**
- One box with EVERYTHING for cars (wheels, body, windows)
- One box with EVERYTHING for trains (wheels, body, smokestack)
- To play with a car, you just open 1 box!

**Vertical Slice Architecture** is like the new way — everything for ONE feature lives together!

## 🔧 Engineer Language

**Vertical Slice Architecture** (VSA) is an architectural pattern where code is organized by feature rather than by technical layer. Each "slice" cuts vertically through all layers (API → Logic → Data).

### Traditional Layered Architecture vs Vertical Slices:

```
┌──────────────────────────────────────────────────────────────────────┐
│                    TRADITIONAL LAYERS                                 │
├──────────────────────────────────────────────────────────────────────┤
│  Controllers/        Services/           Repositories/               │
│  ├── ReceiptCtrl     ├── ReceiptSvc      ├── ReceiptRepo            │
│  ├── RecipeCtrl      ├── RecipeSvc       ├── RecipeRepo             │
│  └── AlertCtrl       └── AlertSvc        └── AlertRepo              │
│                                                                       │
│  To add "ScanReceipt", you touch ALL 3 folders + Models + DTOs!     │
└──────────────────────────────────────────────────────────────────────┘

                              ↓

┌──────────────────────────────────────────────────────────────────────┐
│                    VERTICAL SLICES                                    │
├──────────────────────────────────────────────────────────────────────┤
│  Features/                                                            │
│  ├── ScanReceipt/        ← EVERYTHING for scanning receipts          │
│  │   ├── ScanReceiptCommand.cs                                       │
│  │   ├── ScanReceiptHandler.cs                                       │
│  │   ├── ScanReceiptEndpoint.cs                                      │
│  │   └── ScanReceiptResult.cs                                        │
│  ├── CreateRecipe/       ← EVERYTHING for creating recipes           │
│  │   └── ...                                                          │
│  └── SendAlert/          ← EVERYTHING for sending alerts             │
│      └── ...                                                          │
│                                                                       │
│  To add "ScanReceipt", you touch ONLY 1 folder!                      │
└──────────────────────────────────────────────────────────────────────┘
```

### Key Principles:

| Principle | Description |
|-----------|-------------|
| **Feature Cohesion** | All code for a feature lives together |
| **Minimal Coupling** | Features don't depend on each other |
| **No Premature Abstraction** | Don't create interfaces until needed |
| **CQRS Light** | Separate Commands (write) from Queries (read) |
| **Handlers Over Services** | One handler per request, not god-services |

### Comparison with DDD:

| Aspect | Domain-Driven Design | Vertical Slice Architecture |
|--------|---------------------|----------------------------|
| **Organization** | By domain/bounded context | By feature/use case |
| **Abstractions** | Many interfaces, repositories | Minimal abstractions |
| **Complexity** | High (aggregates, value objects) | Lower (handlers, requests) |
| **Best For** | Complex domains, large teams | CRUD-heavy apps, small teams |
| **Learning Curve** | Steep | Gentle |

> **Note**: VSA doesn't mean NO domain modeling — you can still have rich domain objects. But you organize them by feature, not by layer.

### When to Use Vertical Slices:

✅ **Good Fit:**
- CRUD-heavy applications
- Small to medium teams
- Rapid prototyping
- Microservices
- Applications with clear features

❌ **Consider DDD Instead:**
- Very complex business rules
- Multiple teams need shared domain model
- Heavy domain logic independent of UI

### Nastart Feature Organization:

```
┌────────────────────────────────────────────────────────────────────┐
│                        NASTART FEATURES                             │
├──────────────────┬────────────────┬────────────────┬───────────────┤
│   📸 RECEIPTS    │   🥣 RECIPES   │   💰 PRICES    │   🔔 ALERTS   │
├──────────────────┼────────────────┼────────────────┼───────────────┤
│ • ScanReceipt    │ • CreateRecipe │ • UpdatePrice  │ • GetAlerts   │
│ • MatchItems     │ • GetRecipe    │ • GetPriceHist │ • DismissAlert│
│ • ConfirmPurchase│ • AddIngredient│ • DetectSpike  │ • CreateAlert │
│ • GetPurchases   │ • CalcCost     │                │               │
├──────────────────┴────────────────┴────────────────┴───────────────┤
│  Shared/                                                            │
│  ├── Domain/         ← Entities (Ingredient, Recipe, Purchase)     │
│  ├── Infrastructure/ ← DbContext, External Services                │
│  └── Common/         ← Shared utilities, base classes              │
└────────────────────────────────────────────────────────────────────┘
```

### The Slice Anatomy:

Every feature follows a similar structure using **MediatR**:

```csharp
// 1. REQUEST — What goes in
public record ScanReceiptCommand(
    Guid UserId,
    IFormFile ReceiptImage
) : IRequest<ScanReceiptResult>;

// 2. RESULT — What comes out  
public record ScanReceiptResult(
    Guid PurchaseId,
    List<ScannedItem> Items,
    decimal Total
);

// 3. HANDLER — The logic
public class ScanReceiptHandler : IRequestHandler<ScanReceiptCommand, ScanReceiptResult>
{
    private readonly AppDbContext _db;
    private readonly IOcrService _ocr;
    
    public ScanReceiptHandler(AppDbContext db, IOcrService ocr)
    {
        _db = db;
        _ocr = ocr;
    }
    
    public async Task<ScanReceiptResult> Handle(
        ScanReceiptCommand request, 
        CancellationToken cancellationToken)
    {
        // 1. OCR the image
        var text = await _ocr.ExtractTextAsync(request.ReceiptImage);
        
        // 2. Parse items
        var items = ParseReceiptItems(text);
        
        // 3. Save to database
        var purchase = new Purchase { UserId = request.UserId, Items = items };
        _db.Purchases.Add(purchase);
        await _db.SaveChangesAsync(cancellationToken);
        
        return new ScanReceiptResult(purchase.Id, items, items.Sum(i => i.Total));
    }
}

// 4. ENDPOINT — Wire to API
public static class ScanReceiptEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/receipts/scan", async (
            [FromForm] IFormFile image,
            ClaimsPrincipal user,
            IMediator mediator) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await mediator.Send(new ScanReceiptCommand(userId, image));
            return TypedResults.Created($"/api/purchases/{result.PurchaseId}", result);
        }).WithTags("Receipts");
    }
}
```

### Endpoint Organization with Minimal API:

Using **MapGroup** to organize endpoints by feature area:

```csharp
// Program.cs
var app = builder.Build();

// Group endpoints by feature area
app.MapGroup("/api/receipts")
   .MapReceiptEndpoints()
   .WithTags("Receipts");

app.MapGroup("/api/recipes")
   .MapRecipeEndpoints()
   .WithTags("Recipes");

app.MapGroup("/api/alerts")
   .MapAlertEndpoints()
   .WithTags("Alerts")
   .RequireAuthorization();

app.Run();
```

### Your Task (Day 3):

1. Draw a box for each feature area (Receipts, Recipes, Ingredients, Alerts)
2. List the features/slices in each box
3. Mark dependencies: "ScanReceipt depends on OCR service"
4. Identify shared code: "Both CreateRecipe and GetRecipe need Recipe entity"
5. Ask: "Can I add a new feature without touching existing features?"

---

# Day 4: Feature Glossary & Naming

## 🧒 Explain Like I'm 5

When mom says "snack time!", everyone knows it means cookies and milk. Nobody is confused!

In our app, we want EVERYONE to use the SAME words:
- We ALL say "ScanReceipt" (not "UploadReceipt", "ProcessReceipt", "OCRReceipt")
- We ALL say "Ingredient" (not "item", "product", "material")
- We ALL say "Handler" (not "service", "processor", "manager")

When the baker says "scan receipt" and the programmer writes `ScanReceiptHandler` — they mean the SAME thing!

## 🔧 Engineer Language

**Feature Glossary** (similar to DDD's Ubiquitous Language) is a shared vocabulary used consistently in code, documentation, and conversation. In Vertical Slice Architecture, we add specific terms for our patterns.

### Benefits:

1. **No Translation Layer** — Feature names = folder names = endpoint names
2. **Reduced Miscommunication** — "ScanReceipt" in meeting = `ScanReceipt/` in code
3. **Living Documentation** — Code structure IS the documentation
4. **Onboarding** — New devs understand features by reading folder names

### Nastart VSA Glossary:

| Term | Definition | Code Example |
|------|------------|--------------|
| **Feature** | A single user capability organized as a vertical slice | `Features/ScanReceipt/` |
| **Command** | A request that changes state (Create, Update, Delete) | `CreateRecipeCommand` |
| **Query** | A request that reads state (Get, List, Search) | `GetRecipeCostQuery` |
| **Handler** | The class that processes a Command or Query | `ScanReceiptHandler` |
| **Result** | The output returned by a Handler | `ScanReceiptResult` |
| **Endpoint** | Minimal API route mapped to a Handler via MediatR | `ScanReceiptEndpoint.cs` |
| **Validator** | FluentValidation rules for a Command/Query | `ScanReceiptValidator` |

### Domain Terms (kept from business):

| Term | Definition | Code Example |
|------|------------|--------------|
| **Ingredient** | A trackable item used in recipes | `class Ingredient` |
| **Purchase** | A single shopping transaction with receipt | `class Purchase` |
| **Recipe** | A product formula with ingredients and quantities | `class Recipe` |
| **Recipe Item** | One ingredient entry in a recipe with quantity | `class RecipeItem` |
| **Cost** | Total ingredient expense to produce one unit | `decimal TotalCost` |
| **Margin** | Percentage profit after deducting cost from sell price | `decimal MarginPercent` |
| **Price Spike** | Ingredient price increases >15% from last purchase | Triggers `CreatePriceSpikeAlert` |
| **Low Stock** | Ingredient quantity below minimum threshold | Triggers `CreateLowStockAlert` |

### Naming Conventions:

```csharp
// ✅ GOOD — Clear VSA naming
Features/
├── ScanReceipt/
│   ├── ScanReceiptCommand.cs        // Verb + Noun + Command
│   ├── ScanReceiptHandler.cs        // Verb + Noun + Handler
│   ├── ScanReceiptResult.cs         // Verb + Noun + Result
│   └── ScanReceiptEndpoint.cs       // Verb + Noun + Endpoint

// ❌ BAD — Generic/layered naming
Services/
├── ReceiptService.cs                // God class doing everything
├── ReceiptProcessor.cs              // What does it process?
├── ReceiptManager.cs                // Manager of what?
```

### Command vs Query Naming:

```csharp
// COMMANDS (change state) — Use action verbs
public record ScanReceiptCommand(...) : IRequest<ScanReceiptResult>;
public record CreateRecipeCommand(...) : IRequest<Guid>;
public record UpdateIngredientPriceCommand(...) : IRequest;
public record DeleteAlertCommand(...) : IRequest;

// QUERIES (read state) — Use "Get" or "List"
public record GetRecipeQuery(Guid RecipeId) : IRequest<RecipeDto>;
public record GetAllIngredientsQuery() : IRequest<List<IngredientDto>>;
public record GetPriceHistoryQuery(Guid IngredientId) : IRequest<PriceHistoryDto>;
```

### How Naming Appears in Code:

```csharp
// The code reads like a feature description!
public class CreateRecipeHandler : IRequestHandler<CreateRecipeCommand, Guid>
{
    private readonly AppDbContext _db;
    
    public CreateRecipeHandler(AppDbContext db)
    {
        _db = db;
    }
    
    public async Task<Guid> Handle(
        CreateRecipeCommand command, 
        CancellationToken cancellationToken)
    {
        var recipe = new Recipe
        {
            Name = command.Name,
            SellPrice = command.SellPrice,
            MinimumMargin = command.MinimumMargin
        };
        
        _db.Recipes.Add(recipe);
        await _db.SaveChangesAsync(cancellationToken);
        
        return recipe.Id;
    }
}
```

### Anti-Patterns to Avoid:

```csharp
// ❌ BAD — Generic programming terms
class ItemManager { }
class DataProcessor { }
class GenericService { }
interface IRepository<T> { }  // Premature abstraction!

// ✅ GOOD — Feature-specific terms
record CreateIngredientCommand { }
class CreateIngredientHandler { }
record GetIngredientByIdQuery { }
class GetIngredientByIdHandler { }
```

### Your Task (Day 4):

1. Create/update `docs/glossary.md` with VSA-specific terms
2. Define all feature names using Verb+Noun format
3. Ensure every term maps directly to a folder/class/file name
4. Review with stakeholder: "When I say 'ScanReceipt', is this clear?"

---

# Day 5: GitHub Repository Setup

## 🧒 Explain Like I'm 5

GitHub is like a magical notebook that:
- **Remembers everything** you write
- **Lets friends help** you write together
- **Never loses** your work (even if your computer breaks!)
- **Shows history** — what you wrote yesterday, last week, last year

A "repository" (repo) is ONE notebook for ONE project.

A "monorepo" is a BIG notebook with different sections:
- Section 1: Backend (the brain)
- Section 2: Frontend (the face)
- Section 3: Docs (the instructions)

## 🔧 Engineer Language

### Monorepo Structure for Nastart:

```
nastart/                          # Root repository
├── .github/
│   └── workflows/
│       └── deploy.yml            # CI/CD pipeline
├── backend/                      # .NET 10 solution
│   ├── Nastart.slnx              # New XML solution format!
│   ├── src/
│   └── tests/
├── frontend/                     # Nuxt 4 application
│   ├── nuxt.config.ts
│   └── ...
├── ocr-service/                  # Python PaddleOCR microservice
│   ├── main.py
│   └── requirements.txt
├── docs/                         # Documentation
├── docker-compose.yml            # Local development
├── .gitignore
├── README.md
└── LICENSE
```

### Commands to Execute:

```powershell
# Navigate to your projects folder
cd C:\Users\AU1833\Documents\personal

# Create the repository (if not exists)
mkdir nastart
cd nastart

# Initialize git
git init

# Create folder structure
mkdir backend, frontend, ocr-service, docs
mkdir docs\lessons

# Create essential files
New-Item README.md
New-Item LICENSE
New-Item docker-compose.yml
New-Item .gitignore

# Create GitHub repository and push
gh repo create nastart --private --source=. --push
```

### .gitignore for Nastart:

```gitignore
# .NET
bin/
obj/
*.user
*.suo
.vs/

# Node
node_modules/
.nuxt/
.output/
dist/

# Python
__pycache__/
*.pyc
venv/
.env

# IDE
.idea/
*.swp

# OS
.DS_Store
Thumbs.db

# Secrets
appsettings.*.json
!appsettings.json
!appsettings.Development.json
```

### Your Task (Day 5):

1. Create GitHub account (if needed)
2. Install GitHub CLI: `winget install GitHub.cli`
3. Create repository with folder structure
4. Write initial README.md
5. Push to GitHub

---

# Day 6: .NET Solution Structure — Feature-Based (slnx)

## 🧒 Explain Like I'm 5

Imagine you're building with LEGO:
- **Solution (.slnx)** = The instruction book for the whole LEGO city
- **Project** = One building (but now organized differently!)

**Old Way** (Layered):
- One building for ALL controllers
- One building for ALL services
- One building for ALL data access
- To find anything about "recipes", look in 3 buildings!

**New Way** (Vertical Slices):
- One building with rooms organized by FEATURE
- The "Recipes room" has everything about recipes
- The "Alerts room" has everything about alerts
- To find anything about "recipes", look in ONE place!

## 🔧 Engineer Language

### Vertical Slice Architecture Project Structure:

In VSA, we have fewer projects but more organized folders:

```
┌──────────────────────────────────────────────────────────────┐
│                         Nastart.Api                          │
│  (Program.cs, Features/, Shared/, Endpoints)                 │
│  Contains: ALL FEATURES organized by use case                │
├──────────────────────────────────────────────────────────────┤
│                     Nastart.Domain (Optional)                │
│  (Entities, Value Objects — if you need rich domain logic)   │
│  Note: Many VSA apps keep entities IN the Api project        │
├──────────────────────────────────────────────────────────────┤
│                    Nastart.Bot (Optional)                    │
│  (Telegram handlers, separate entry point)                   │
│  Can share code with Api via shared Features                 │
└──────────────────────────────────────────────────────────────┘
```

### Key Difference from Layered:

| Layered Architecture | Vertical Slice Architecture |
|---------------------|---------------------------|
| 5+ projects (Api, Domain, Application, Infrastructure, Contracts) | 1-3 projects (Api, optional Domain, optional Bot) |
| `Controllers/RecipeController.cs` | `Features/Recipes/CreateRecipe/Endpoint.cs` |
| `Services/RecipeService.cs` | `Features/Recipes/CreateRecipe/Handler.cs` |
| `Repositories/IRecipeRepository.cs` | Direct DbContext in Handler |
| Many interfaces | Few/no interfaces (YAGNI) |

### Why `.slnx` over `.sln`?

| Aspect | `.sln` (Legacy) | `.slnx` (.NET 10 default) |
|--------|-----------------|---------------------------|
| Format | Proprietary text | Clean XML |
| Readability | Cryptic GUIDs | Human-readable |
| Git Merge | Conflict-prone | Easy to merge |
| Size | Verbose | Compact |

> **Note**: In .NET 10, `dotnet new sln` automatically creates `.slnx` format!

### Recommended Project Structure:

```
Nastart.Api/
├── Features/                          # All vertical slices
│   ├── Receipts/
│   │   ├── ScanReceipt/
│   │   │   ├── ScanReceiptCommand.cs
│   │   │   ├── ScanReceiptHandler.cs
│   │   │   ├── ScanReceiptResult.cs
│   │   │   ├── ScanReceiptValidator.cs
│   │   │   └── ScanReceiptEndpoint.cs
│   │   ├── GetPurchaseHistory/
│   │   │   ├── GetPurchaseHistoryQuery.cs
│   │   │   ├── GetPurchaseHistoryHandler.cs
│   │   │   └── GetPurchaseHistoryEndpoint.cs
│   │   └── _ReceiptEndpoints.cs       # MapGroup for all receipt endpoints
│   │
│   ├── Recipes/
│   │   ├── CreateRecipe/
│   │   ├── GetRecipe/
│   │   ├── GetAllRecipes/
│   │   ├── AddIngredientToRecipe/
│   │   └── _RecipeEndpoints.cs
│   │
│   ├── Ingredients/
│   │   ├── CreateIngredient/
│   │   ├── GetIngredient/
│   │   ├── UpdatePrice/
│   │   └── _IngredientEndpoints.cs
│   │
│   └── Alerts/
│       ├── GetAlerts/
│       ├── DismissAlert/
│       └── _AlertEndpoints.cs
│
├── Domain/                            # Domain entities (or separate project)
│   ├── Ingredient.cs
│   ├── Recipe.cs
│   ├── RecipeItem.cs
│   ├── Purchase.cs
│   ├── PurchaseItem.cs
│   └── Alert.cs
│
├── Shared/                            # Cross-cutting concerns
│   ├── Infrastructure/
│   │   ├── AppDbContext.cs
│   │   ├── Configurations/           # EF Core configurations
│   │   └── Migrations/
│   ├── Services/
│   │   ├── IOcrService.cs
│   │   └── OcrService.cs
│   └── Behaviors/                     # MediatR pipeline behaviors
│       ├── ValidationBehavior.cs
│       └── LoggingBehavior.cs
│
├── Program.cs                         # Entry point, DI, middleware
├── appsettings.json
└── Nastart.Api.csproj
```

### Commands to Create Solution:

> ✅ **Verified with .NET 10.0.101** — All commands below are from official Microsoft documentation.
> See: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend

# Create solution (.slnx is the default format in .NET 10)
dotnet new sln -n Nastart

# Create main API project (contains Features)
dotnet new webapi -n Nastart.Api -o src/Nastart.Api --use-minimal-apis

# Create optional Bot project
dotnet new worker -n Nastart.Bot -o src/Nastart.Bot

# Create test projects
dotnet new xunit -n Nastart.Api.Tests -o tests/Nastart.Api.Tests

# Add projects to solution
dotnet sln add src/Nastart.Api/Nastart.Api.csproj
dotnet sln add src/Nastart.Bot/Nastart.Bot.csproj
dotnet sln add tests/Nastart.Api.Tests/Nastart.Api.Tests.csproj

# Add test reference
dotnet add tests/Nastart.Api.Tests/Nastart.Api.Tests.csproj reference src/Nastart.Api/Nastart.Api.csproj

# Add required packages to Api project
cd src/Nastart.Api
dotnet add package MediatR
dotnet add package FluentValidation.DependencyInjectionExtensions
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

### What Nastart.slnx Looks Like:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/Nastart.Api/Nastart.Api.csproj" />
    <Project Path="src/Nastart.Bot/Nastart.Bot.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/Nastart.Api.Tests/Nastart.Api.Tests.csproj" />
  </Folder>
</Solution>
```

### Program.cs Setup for Vertical Slices:

```csharp
using MediatR;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add MediatR (scans for handlers in this assembly)
builder.Services.AddMediatR(cfg => 
    cfg.RegisterServicesFromAssemblyContaining<Program>());

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Add our services
builder.Services.AddScoped<IOcrService, OcrService>();

// Add OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map feature endpoints using MapGroup
app.MapGroup("/api/receipts")
   .MapReceiptEndpoints()
   .WithTags("Receipts");

app.MapGroup("/api/recipes")
   .MapRecipeEndpoints()
   .WithTags("Recipes");

app.MapGroup("/api/ingredients")
   .MapIngredientEndpoints()
   .WithTags("Ingredients");

app.MapGroup("/api/alerts")
   .MapAlertEndpoints()
   .WithTags("Alerts");

app.Run();
```

### Example Feature Endpoint Registration:

```csharp
// Features/Recipes/_RecipeEndpoints.cs
public static class RecipeEndpointExtensions
{
    public static RouteGroupBuilder MapRecipeEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateRecipeEndpoint.Handle)
             .WithName("CreateRecipe");
        
        group.MapGet("/{id:guid}", GetRecipeEndpoint.Handle)
             .WithName("GetRecipe");
        
        group.MapGet("/", GetAllRecipesEndpoint.Handle)
             .WithName("GetAllRecipes");
        
        group.MapPost("/{id:guid}/ingredients", AddIngredientToRecipeEndpoint.Handle)
             .WithName("AddIngredientToRecipe");
        
        return group;
    }
}

// Features/Recipes/CreateRecipe/CreateRecipeEndpoint.cs
public static class CreateRecipeEndpoint
{
    public static async Task<IResult> Handle(
        CreateRecipeCommand command,
        IMediator mediator)
    {
        var id = await mediator.Send(command);
        return TypedResults.Created($"/api/recipes/{id}", new { id });
    }
}
```

### Verify Solution:

```powershell
dotnet build
dotnet test
```

---

# Day 7: Docker & PostgreSQL with Best Practices

## 🧒 Explain Like I'm 5

Docker is like a **lunchbox** for computer programs:
- Your sandwich (program) stays fresh
- It doesn't mix with your friend's sandwich
- You can share the exact same lunchbox with anyone!

But we need to be SMART about our lunchboxes:
- **Small lunchbox** = faster to carry (optimized image)
- **Lock on lunchbox** = keeps it safe (security)
- **Check inside** = make sure food is good (health checks)

PostgreSQL is a **super-smart filing cabinet** that:
- Remembers ALL your data
- Finds things super fast
- Never forgets (even if you turn off the computer)

We put PostgreSQL INSIDE a Docker lunchbox, so everyone on our team has the exact same filing cabinet!

## 🔧 Engineer Language

### Why Docker for Local Dev:

1. **Consistent Environment** — Works the same on every machine
2. **No Installation Hassle** — No "it works on my machine"
3. **Easy Cleanup** — Delete container, fresh start
4. **Multiple Versions** — Run PostgreSQL 18 alongside other versions

### Step 1: Create .dockerignore (Build Context Optimization)

Before building, we MUST exclude unnecessary files to speed up builds and keep images small:

```gitignore
# .dockerignore — Place in project root
# .NET
bin/
obj/
*.user
*.suo
.vs/

# Node
node_modules/
.nuxt/
.output/
dist/

# Python
__pycache__/
*.pyc
venv/

# IDE
.idea/
*.swp
.vscode/

# OS
.DS_Store
Thumbs.db

# Secrets (NEVER include in build context!)
.env
appsettings.*.json
!.dockerignore

# Git
.git/
.gitignore

# Documentation (not needed in containers)
*.md
docs/
```

### Step 2: Optimized .NET API Dockerfile (Multi-Stage Build)

Create `backend/src/Nastart.Api/Dockerfile` with production-ready patterns:

```dockerfile
# ==========================================
# STAGE 1: Build
# Restore and compile in one stage for reliability
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy only project files first (layer caching optimization)
COPY src/Nastart.Domain/Nastart.Domain.csproj Nastart.Domain/
COPY src/Nastart.Application/Nastart.Application.csproj Nastart.Application/
COPY src/Nastart.Infrastructure/Nastart.Infrastructure.csproj Nastart.Infrastructure/
COPY src/Nastart.Api/Nastart.Api.csproj Nastart.Api/

# Restore dependencies (cached unless .csproj changes)
RUN dotnet restore Nastart.Api/Nastart.Api.csproj

# Copy source code
COPY src/ .

# Build in Release mode (allow implicit restore since COPY overwrites obj folders)
RUN dotnet build Nastart.Api/Nastart.Api.csproj -c Release

# ==========================================
# STAGE 2: Publish
# Prepare runtime artifacts
# ==========================================
FROM build AS publish
WORKDIR /src
RUN dotnet publish Nastart.Api/Nastart.Api.csproj -c Release -o /app/publish --no-build

# ==========================================
# STAGE 3: Runtime (Production)
# Minimal, secure runtime image
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime

# Security: Create non-root user
RUN addgroup -g 1001 -S appgroup && \
    adduser -S appuser -u 1001 -G appgroup

WORKDIR /app

# Copy published artifacts with correct ownership
COPY --from=publish --chown=appuser:appgroup /app/publish .

# Security: Run as non-root user
USER 1001

# Health check for container monitoring
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

EXPOSE 8080

ENTRYPOINT ["dotnet", "Nastart.Api.dll"]
```

### Step 3: Production-Ready docker-compose.yml

Create `docker-compose.yml` with orchestration best practices:

```yaml
# Docker Compose for Nastart development environment

services:
  # ==========================================
  # PostgreSQL Database
  # ==========================================
  postgres:
    image: postgres:18-alpine
    container_name: nastart-db
    restart: unless-stopped
    environment:
      POSTGRES_USER: nastart
      POSTGRES_PASSWORD: nastart_dev_password
      POSTGRES_DB: nastart
    ports:
      - "5432:5432"
    volumes:
      - nastart_postgres_data:/var/lib/postgresql/data
    networks:
      - nastart-backend
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U nastart"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 10s
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 1G
        reservations:
          cpus: '0.5'
          memory: 512M

  # ==========================================
  # .NET API Service
  # ==========================================
  api:
    build:
      context: ./backend
      dockerfile: src/Nastart.Api/Dockerfile
      target: runtime
    container_name: nastart-api
    restart: unless-stopped
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=nastart;Username=nastart;Password=nastart_dev_password
    ports:
      - "5000:8080"
    depends_on:
      postgres:
        condition: service_healthy
    networks:
      - nastart-backend
      - nastart-frontend
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M

  # ==========================================
  # PaddleOCR Service
  # ==========================================
  ocr:
    build:
      context: ./ocr-service
      dockerfile: Dockerfile
    container_name: nastart-ocr
    restart: unless-stopped
    environment:
      - PYTHONUNBUFFERED=1
      - PORT=8001
    ports:
      - "8001:8001"
    networks:
      - nastart-backend
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8001/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 60s
    deploy:
      resources:
        limits:
          cpus: '1.0'
          memory: 2G
        reservations:
          cpus: '0.5'
          memory: 1G

networks:
  nastart-backend:
    driver: bridge
  nastart-frontend:
    driver: bridge

volumes:
  nastart_postgres_data:
    driver: local
```

### Step 4: Development Override (docker-compose.override.yml)

For development with hot reload, create `docker-compose.override.yml`:

```yaml
# Docker Compose for Nastart development environment

services:
  api:
    build:
      target: build  # Use build stage for dev
    volumes:
      - ./backend/src:/src:ro  # Mount source code read-only
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_URLS=http://+:8080
    command: dotnet watch run --project src/Nastart.Api/Nastart.Api.csproj
    ports:
      - "5000:8080"
      - "9229:9229"  # Debug port
```

### Step 5: Commands to Run

```powershell
# Install Docker Desktop for Windows (if not installed)
winget install Docker.DockerDesktop

# Verify Docker installation
docker --version
docker info | findstr "Server Version"

# Build and start services
cd C:\Users\AU1833\Documents\personal\nastart
docker-compose up -d --build

# Check container status
docker-compose ps

# View logs (follow mode)
docker-compose logs -f

# View specific service logs
docker-compose logs -f api
docker-compose logs -f postgres

# Connect to PostgreSQL
docker exec -it nastart-db psql -U nastart -d nastart

# Stop services (keep data)
docker-compose down

# Stop and remove all data (CAREFUL!)
docker-compose down -v

# Validate compose configuration
docker-compose config

# Check image sizes
docker images --format "table {{.Repository}}\t{{.Tag}}\t{{.Size}}"

# Inspect container health
docker inspect --format='{{.State.Health.Status}}' nastart-api
```

### Step 6: Connection String for .NET

```json
// appsettings.Development.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres;Port=5432;Database=nastart;Username=nastart;Password=nastart_dev_password"
  }
}
```

Note: Use `Host=postgres` (service name) when running inside Docker network, `Host=localhost` when running API outside Docker.

### Step 7: Install EF Core Tools

```powershell
# Install EF Core CLI tool
dotnet tool install --global dotnet-ef

# Verify installation
dotnet ef --version
```

### Docker Expert Checklist

Before considering your Docker setup complete, verify:

#### Dockerfile Optimization
- [ ] Dependencies copied before source code for optimal layer caching
- [ ] Multi-stage builds separate build and runtime environments
- [ ] Production stage only includes necessary artifacts
- [ ] Build context optimized with comprehensive .dockerignore
- [ ] Base image uses Alpine for smaller size (mcr.microsoft.com/dotnet/aspnet:10.0-alpine)

#### Container Security Hardening
- [ ] Non-root user created with specific UID/GID (1001)
- [ ] Container runs as non-root user (USER 1001 directive)
- [ ] Secrets managed via environment files (not committed to git)
- [ ] Minimal attack surface (Alpine-based images)
- [ ] Health checks implemented for container monitoring

#### Docker Compose Orchestration
- [ ] Service dependencies properly defined with health checks (condition: service_healthy)
- [ ] Custom networks configured for service isolation (nastart-backend, nastart-frontend)
- [ ] Environment-specific configurations separated (dev override file)
- [ ] Volume strategies appropriate for data persistence (named volumes)
- [ ] Resource limits defined to prevent resource exhaustion
- [ ] Restart policies configured for production resilience (unless-stopped)

#### Image Size & Performance
- [ ] Final image size optimized (Alpine base, multi-stage)
- [ ] Build cache optimization via proper layer ordering
- [ ] Artifact copying selective (only required files)

### Your Task (Day 7):

1. **Install Docker Desktop** and verify installation
2. **Create .dockerignore** in project root (comprehensive exclusions)
3. **Create optimized Dockerfile** for Nastart.Api with multi-stage builds
4. **Create docker-compose.yml** with health checks, networks, and resource limits
5. **Create docker-compose.override.yml** for development workflow
6. **Build and run**: `docker-compose up -d --build`
7. **Verify health checks**: `docker ps` should show containers as "healthy"
8. **Connect to PostgreSQL** and verify connection
9. **Review image sizes**: Run `docker images` and ensure API image < 200MB
10. **Validate checklist**: Ensure all security and optimization items are complete

---

# Resources

> ✅ All links verified as of January 2026

## Vertical Slice Architecture
| Resource | Type | Link |
|----------|------|------|
| **Organizing ASP.NET Core Minimal APIs** | Article | https://www.tessferrandez.com/blog/2023/10/31/organizing-minimal-apis.html |
| Vertical Slice Architecture (Jimmy Bogard) | Video | https://www.youtube.com/watch?v=SUiWfhAhgQw |
| ContosoUniversity VSA Example | GitHub | https://github.com/jbogard/ContosoUniversityDotNetCore-Pages |
| Feature Folders in ASP.NET Core | Article | https://scottsauber.com/2016/04/25/feature-folder-structure-in-asp-net-core/ |

## Minimal APIs (Microsoft Official)
| Resource | Type | Link |
|----------|------|------|
| **Minimal APIs quick reference** | MS Docs | https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis |
| **Tutorial: Create a Minimal API** | MS Docs | https://learn.microsoft.com/en-us/aspnet/core/tutorials/min-web-api |
| **Minimal API route handlers** | MS Docs | https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers |
| **Parameter binding in Minimal APIs** | MS Docs | https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/parameter-binding |
| **Create responses in Minimal API apps** | MS Docs | https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses |
| **Filters in Minimal API apps** | MS Docs | https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/min-api-filters |

## MediatR & CQRS
| Resource | Type | Link |
|----------|------|------|
| MediatR Documentation | GitHub Wiki | https://github.com/jbogard/MediatR/wiki |
| MediatR Library | GitHub | https://github.com/jbogard/MediatR |
| **Implement command pipeline with MediatR** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api |

## Common Web Application Architectures (Microsoft Official)
| Resource | Type | Link |
|----------|------|------|
| **Common web application architectures** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures |
| **Clean Architecture** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures#clean-architecture |
| **Architect Modern Web Apps** | MS eBook | https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/ |

## .NET 10 & CLI (Microsoft Official)
| Resource | Type | Link |
|----------|------|------|
| **dotnet new command** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new |
| **Default templates for dotnet new** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new-sdk-templates |
| **dotnet sln command** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln |
| **dotnet add reference** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-add-reference |
| **What's new in .NET 10** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview |
| **dotnet new sln defaults to SLNX** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default |

## Entity Framework Core & PostgreSQL (Microsoft Official)
| Resource | Type | Link |
|----------|------|------|
| **Entity Framework Core** | MS Docs | https://learn.microsoft.com/en-us/ef/core/ |
| **Getting Started with EF Core** | MS Docs | https://learn.microsoft.com/en-us/ef/core/get-started/overview/first-app |
| **EF Core - ASP.NET MVC Tutorial** | MS Docs | https://learn.microsoft.com/en-us/aspnet/core/data/ef-mvc/intro |
| Npgsql EF Core Provider | Docs | https://www.npgsql.org/efcore/ |

## Docker & Containers (Microsoft Official)
| Resource | Type | Link |
|----------|------|------|
| **Introduction to .NET and Docker** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/docker/introduction |
| **Tutorial: Containerize a .NET app** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/docker/build-container |
| **Use a database server running as a container** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/database-server-container |
| **Development workflow for Docker apps** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/docker-application-development-process/docker-app-development-workflow |
| Docker Desktop | Tool | https://docs.docker.com/desktop/ |

## Validation with FluentValidation
| Resource | Type | Link |
|----------|------|------|
| FluentValidation Docs | Official | https://docs.fluentvalidation.net/ |
| FluentValidation with MediatR | GitHub | https://github.com/FluentValidation/FluentValidation |

## eBooks (Free Downloads from Microsoft)
| Resource | Description | Link |
|----------|-------------|------|
| **Architect Modern Web Apps with ASP.NET Core** | Architecture patterns | https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/ |
| **.NET Microservices Architecture** | CQRS patterns (still relevant for VSA) | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/ |

## Tools
| Tool | Purpose | Link |
|------|---------|------|
| Miro | Feature mapping online | https://miro.com |
| FigJam | Collaborative whiteboard | https://www.figma.com/figjam/ |
| draw.io | Diagrams | https://www.drawio.com/ |
| GitHub Desktop | Git GUI | https://desktop.github.com/ |
| DBeaver | PostgreSQL GUI | https://dbeaver.io/ |
| pgAdmin | PostgreSQL Admin | https://www.pgadmin.org/ |

---

# Week 1 Checklist

- [ ] **Day 1-2**: Complete Feature Discovery exercise
  - [ ] List 15+ user capabilities (things users want to DO)
  - [ ] Group by feature area (Receipts, Recipes, Ingredients, Alerts)
  - [ ] Mark each as Command or Query
  - [ ] Name using Verb+Noun format
  - [ ] Take photo/screenshot for reference

- [ ] **Day 3**: Understand Vertical Slice Architecture
  - [ ] Draw feature map with all slices
  - [ ] Understand Command vs Query pattern
  - [ ] Sketch a single slice structure (Command, Handler, Result, Endpoint)
  - [ ] Compare with traditional layered architecture

- [ ] **Day 4**: Write Feature Glossary
  - [ ] Create/update `docs/glossary.md` with VSA terms
  - [ ] Define all feature names
  - [ ] Verify naming convention: Verb+Noun
  - [ ] Ensure terms match code folder/class names

- [ ] **Day 5**: GitHub Repository
  - [ ] Create monorepo structure
  - [ ] Write README.md
  - [ ] Add .gitignore
  - [ ] Push to GitHub

- [ ] **Day 6**: .NET Solution — Feature-Based (slnx)
  - [ ] Create solution with slnx format
  - [ ] Create Nastart.Api project with Minimal APIs
  - [ ] Set up Features/ folder structure
  - [ ] Add MediatR and FluentValidation packages
  - [ ] Create test project
  - [ ] Verify `dotnet build` works

- [ ] **Day 7**: Docker & PostgreSQL with Best Practices
  - [ ] Install Docker Desktop and verify installation
  - [ ] Create comprehensive .dockerignore
  - [ ] Create optimized multi-stage Dockerfile for API
  - [ ] Create docker-compose.yml with health checks & resource limits
  - [ ] Create docker-compose.override.yml for development
  - [ ] Build and run: `docker-compose up -d --build`
  - [ ] Verify all containers show as "healthy"
  - [ ] Connect to PostgreSQL and test connection
  - [ ] Review image sizes (target: API < 200MB)
  - [ ] Complete Docker Expert Checklist validation

---

**Next Week**: [Week 2 - Building Your First Features](./week-02-domain-foundation.md)

---

## Quick Reference: Key Microsoft Docs

For quick access during development, bookmark these essential Microsoft docs:

```
Minimal APIs:
├── Quick reference: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis
├── Tutorial: https://learn.microsoft.com/en-us/aspnet/core/tutorials/min-web-api
├── Route handlers: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers
└── Responses: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses

.NET CLI Commands:
├── dotnet new: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new
├── dotnet sln: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln
├── dotnet add reference: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-add-reference
└── All templates: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new-sdk-templates

Architecture:
├── Common architectures: https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures
└── Modern Web Apps eBook: https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/

.NET 10 What's New:
└── https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview

EF Core Getting Started:
└── https://learn.microsoft.com/en-us/ef/core/get-started/overview/first-app

Docker + .NET:
└── https://learn.microsoft.com/en-us/dotnet/core/docker/introduction
```

### Key Packages for Vertical Slice Architecture

| Package | Purpose | NuGet |
|---------|---------|-------|
| MediatR | Request/Handler pattern | `dotnet add package MediatR` |
| FluentValidation | Input validation | `dotnet add package FluentValidation.DependencyInjectionExtensions` |
| Npgsql.EntityFrameworkCore.PostgreSQL | PostgreSQL provider | `dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL` |
| Microsoft.EntityFrameworkCore.Design | EF Core tooling | `dotnet add package Microsoft.EntityFrameworkCore.Design` |

### Available Templates (dotnet new list)

| Template | Short Name | Description |
|----------|------------|-------------|
| Solution File | `sln` | Empty solution (slnx in .NET 10) |
| ASP.NET Core Web API | `webapi` | Web API with Minimal APIs |
| Class Library | `classlib` | Reusable class library |
| Console App | `console` | Console application |
| xUnit Test Project | `xunit` | Unit test project with xUnit |
| Worker Service | `worker` | Background worker service |

Run `dotnet new list` to see all available templates on your system.

---

*Happy learning! Remember: Features, not layers — organize by what users DO, not how the code works.* 🚀
