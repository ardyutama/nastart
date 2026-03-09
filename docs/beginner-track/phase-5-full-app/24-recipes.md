# Lesson 24: Recipes — Creating Recipes with Ingredients and a Status State Machine

> **Phase**: 5 — Building the Full Application
> **Prerequisite**: Lesson 23. The `ingredients` table has `user_id` and tracks price history.
> **What you build**: `POST /recipes` creates a recipe with linked ingredients. A recipe has a `status` field that moves through a defined state machine.

---

## What You Will Learn

- How to model a many-to-many relationship using a junction table (`recipe_ingredients`)
- How to validate that referenced records exist before writing
- How to implement a status state machine using an `enum`

---

## The Schema

```sql
CREATE TABLE recipes (
    id              UUID PRIMARY KEY,
    user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name            TEXT NOT NULL,
    status          TEXT NOT NULL DEFAULT 'draft',   -- 'draft' | 'active' | 'archived'
    selling_price   NUMERIC(12,4),
    last_cost       NUMERIC(12,4),                   -- updated by Lesson 25
    margin_percent  NUMERIC(7,4),                    -- updated by Lesson 25
    created_at      TIMESTAMP WITH TIME ZONE NOT NULL
);

CREATE TABLE recipe_ingredients (
    id            UUID PRIMARY KEY,
    recipe_id     UUID NOT NULL REFERENCES recipes(id) ON DELETE CASCADE,
    ingredient_id UUID NOT NULL REFERENCES ingredients(id) ON DELETE RESTRICT,
    quantity      NUMERIC(12,3) NOT NULL,
    unit          TEXT NOT NULL
);
```

---

## Step 1 — Create the Recipe and RecipeIngredient Entities

Create `src/Nastart.Api/Features/Recipes/Recipe.cs`:

```csharp
namespace Nastart.Api.Features.Recipes;

public enum RecipeStatus { Draft, Active, Archived }

public sealed class Recipe
{
    public Guid          Id            { get; set; }
    public Guid          UserId        { get; set; }
    public string        Name          { get; set; } = string.Empty;
    public RecipeStatus  Status        { get; set; } = RecipeStatus.Draft;
    public decimal?      SellingPrice  { get; set; }
    public decimal?      LastCost      { get; set; }
    public decimal?      MarginPercent { get; set; }
    public DateTimeOffset CreatedAt    { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public User? User { get; set; }
    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = [];
}
```

Create `src/Nastart.Api/Features/Recipes/RecipeIngredient.cs`:

```csharp
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Features.Recipes;

public sealed class RecipeIngredient
{
    public Guid       Id           { get; set; }
    public Guid       RecipeId     { get; set; }
    public Guid       IngredientId { get; set; }
    public decimal    Quantity     { get; set; }
    public string     Unit         { get; set; } = string.Empty;

    // Navigation
    public Recipe?     Recipe     { get; set; }
    public Ingredient? Ingredient { get; set; }
}
```

---

## Step 2 — EF Core Configuration

Create `src/Nastart.Api/Features/Recipes/RecipeConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nastart.Api.Features.Recipes;

public sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Status)
               .HasConversion<string>();   // store as TEXT, not integer
        builder.Property(r => r.SellingPrice).HasPrecision(12, 4);
        builder.Property(r => r.LastCost).HasPrecision(12, 4);
        builder.Property(r => r.MarginPercent).HasPrecision(7, 4);

        builder.HasOne(r => r.User)
               .WithMany()
               .HasForeignKey(r => r.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.UserId);
    }
}

public sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.HasKey(ri => ri.Id);
        builder.Property(ri => ri.Quantity).HasPrecision(12, 3);
        builder.Property(ri => ri.Unit).IsRequired().HasMaxLength(50);

        // ON DELETE CASCADE: removing a recipe removes its ingredient lines
        builder.HasOne(ri => ri.Recipe)
               .WithMany(r => r.RecipeIngredients)
               .HasForeignKey(ri => ri.RecipeId)
               .OnDelete(DeleteBehavior.Cascade);

        // ON DELETE RESTRICT: you cannot delete an ingredient that is used in a recipe
        builder.HasOne(ri => ri.Ingredient)
               .WithMany()
               .HasForeignKey(ri => ri.IngredientId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
```

> **`HasConversion<string>()`** stores the enum as `"Draft"`, `"Active"`, `"Archived"` in the database — much more readable than the integer `0`, `1`, `2`.

Add the `DbSet`s to `NastartDbContext`:

```csharp
public DbSet<Recipe>           Recipes           => Set<Recipe>();
public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
```

---

## Step 3 — CreateRecipe Handler

Create `src/Nastart.Api/Features/Recipes/CreateRecipe.cs`:

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Services;

namespace Nastart.Api.Features.Recipes;

// ── Request ────────────────────────────────────────────────────────

public sealed record RecipeIngredientInput(
    Guid    IngredientId,
    decimal Quantity,
    string  Unit);

public sealed record CreateRecipeCommand(
    string                       Name,
    decimal?                     SellingPrice,
    List<RecipeIngredientInput>  Ingredients)
    : IRequest<RecipeDetailResponse>;

// ── Validator ──────────────────────────────────────────────────────

public sealed class CreateRecipeValidator : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SellingPrice).GreaterThan(0).When(x => x.SellingPrice.HasValue);
        RuleFor(x => x.Ingredients).NotEmpty().WithMessage("A recipe must have at least one ingredient.");
        RuleForEach(x => x.Ingredients).ChildRules(i =>
        {
            i.RuleFor(x => x.Quantity).GreaterThan(0);
            i.RuleFor(x => x.Unit).NotEmpty().MaximumLength(50);
        });
    }
}

// ── Response ───────────────────────────────────────────────────────

public sealed record RecipeLineResponse(
    Guid    IngredientId,
    string  IngredientName,
    decimal Quantity,
    string  Unit);

public sealed record RecipeDetailResponse(
    Guid                      Id,
    string                    Name,
    string                    Status,
    decimal?                  SellingPrice,
    decimal?                  LastCost,
    decimal?                  MarginPercent,
    DateTimeOffset            CreatedAt,
    List<RecipeLineResponse>  Ingredients);

// ── Domain Exception ───────────────────────────────────────────────

public sealed class IngredientNotOwnedException(Guid id)
    : Exception($"Ingredient {id} does not belong to you or does not exist.");

// ── Handler ────────────────────────────────────────────────────────

public sealed class CreateRecipeHandler(NastartDbContext db, ICurrentUser currentUser)
    : IRequestHandler<CreateRecipeCommand, RecipeDetailResponse>
{
    public async Task<RecipeDetailResponse> Handle(
        CreateRecipeCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate that all referenced ingredients exist and belong to the current user
        var requestedIds = request.Ingredients.Select(i => i.IngredientId).Distinct().ToList();
        var ownedIds = await db.Ingredients
            .Where(i => requestedIds.Contains(i.Id) && i.UserId == currentUser.UserId)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken);

        var missingId = requestedIds.FirstOrDefault(id => !ownedIds.Contains(id));
        if (missingId != Guid.Empty && !ownedIds.Contains(missingId))
            throw new IngredientNotOwnedException(missingId);

        // 2. Build the recipe aggregate
        var recipe = new Recipe
        {
            Id           = Guid.NewGuid(),
            UserId       = currentUser.UserId,
            Name         = request.Name,
            Status       = RecipeStatus.Draft,
            SellingPrice = request.SellingPrice,
            CreatedAt    = DateTimeOffset.UtcNow,
            RecipeIngredients = request.Ingredients
                .Select(i => new RecipeIngredient
                {
                    Id           = Guid.NewGuid(),
                    IngredientId = i.IngredientId,
                    Quantity     = i.Quantity,
                    Unit         = i.Unit
                })
                .ToList()
        };

        db.Recipes.Add(recipe);
        await db.SaveChangesAsync(cancellationToken);

        // 3. Re-load with ingredient names for the response
        await db.Entry(recipe)
            .Collection(r => r.RecipeIngredients)
            .Query()
            .Include(ri => ri.Ingredient)
            .LoadAsync(cancellationToken);

        return MapToResponse(recipe);
    }

    private static RecipeDetailResponse MapToResponse(Recipe recipe) =>
        new(recipe.Id,
            recipe.Name,
            recipe.Status.ToString(),
            recipe.SellingPrice,
            recipe.LastCost,
            recipe.MarginPercent,
            recipe.CreatedAt,
            recipe.RecipeIngredients
                .Select(ri => new RecipeLineResponse(
                    ri.IngredientId,
                    ri.Ingredient?.Name ?? string.Empty,
                    ri.Quantity,
                    ri.Unit))
                .ToList());
}
```

---

## Step 4 — Status State Machine

Recipes move through a simple lifecycle: `Draft → Active → Archived`.

Create `src/Nastart.Api/Features/Recipes/UpdateRecipeStatus.cs`:

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Services;

namespace Nastart.Api.Features.Recipes;

public sealed record UpdateRecipeStatusCommand(Guid RecipeId, string NewStatus)
    : IRequest<RecipeDetailResponse>;

public sealed class UpdateRecipeStatusValidator : AbstractValidator<UpdateRecipeStatusCommand>
{
    private static readonly string[] AllowedStatuses = ["Draft", "Active", "Archived"];

    public UpdateRecipeStatusValidator()
    {
        RuleFor(x => x.NewStatus)
            .Must(s => AllowedStatuses.Contains(s))
            .WithMessage("Status must be Draft, Active, or Archived.");
    }
}

public sealed class RecipeNotFoundException(Guid id)
    : Exception($"Recipe {id} not found.");

public sealed class InvalidStatusTransitionException(string from, string to)
    : Exception($"Cannot transition recipe from {from} to {to}.");

public sealed class UpdateRecipeStatusHandler(NastartDbContext db, ICurrentUser currentUser)
    : IRequestHandler<UpdateRecipeStatusCommand, RecipeDetailResponse>
{
    // Valid transitions: which statuses can you move TO from each current status
    private static readonly Dictionary<RecipeStatus, RecipeStatus[]> AllowedTransitions = new()
    {
        [RecipeStatus.Draft]    = [RecipeStatus.Active, RecipeStatus.Archived],
        [RecipeStatus.Active]   = [RecipeStatus.Archived],
        [RecipeStatus.Archived] = []   // terminal state — no transitions out
    };

    public async Task<RecipeDetailResponse> Handle(
        UpdateRecipeStatusCommand request,
        CancellationToken cancellationToken)
    {
        var recipe = await db.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(
                r => r.Id == request.RecipeId && r.UserId == currentUser.UserId,
                cancellationToken);

        if (recipe is null)
            throw new RecipeNotFoundException(request.RecipeId);

        var targetStatus = Enum.Parse<RecipeStatus>(request.NewStatus);
        var allowed = AllowedTransitions[recipe.Status];

        if (!allowed.Contains(targetStatus))
            throw new InvalidStatusTransitionException(
                recipe.Status.ToString(), request.NewStatus);

        recipe.Status = targetStatus;
        await db.SaveChangesAsync(cancellationToken);

        return MapToDetailResponse(recipe);
    }

    private static RecipeDetailResponse MapToDetailResponse(Recipe recipe) =>
        new(recipe.Id, recipe.Name, recipe.Status.ToString(),
            recipe.SellingPrice, recipe.LastCost, recipe.MarginPercent, recipe.CreatedAt,
            recipe.RecipeIngredients
                .Select(ri => new RecipeLineResponse(
                    ri.IngredientId, ri.Ingredient?.Name ?? string.Empty,
                    ri.Quantity, ri.Unit))
                .ToList());
}
```

---

## Step 5 — Register Endpoints

Create `src/Nastart.Api/Features/Recipes/RecipesEndpoints.cs`:

```csharp
using MediatR;

namespace Nastart.Api.Features.Recipes;

public static class RecipesEndpoints
{
    public static void MapRecipesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/recipes")
            .RequireAuthorization()
            .WithTags("Recipes");

        group.MapPost("/", async (CreateRecipeCommand command, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return TypedResults.Created($"/recipes/{result.Id}", result);
        }).WithName("CreateRecipe").WithSummary("Create a new recipe");

        group.MapPatch("/{id}/status", async (
            Guid id,
            UpdateStatusRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new UpdateRecipeStatusCommand(id, body.Status), ct);
            return TypedResults.Ok(result);
        }).WithName("UpdateRecipeStatus").WithSummary("Move recipe through the status workflow");
    }
}

internal sealed record UpdateStatusRequest(string Status);
```

Register in `Program.cs`:

```csharp
app.MapRecipesEndpoints();
```

Add new exceptions to the global exception handler:

```csharp
RecipeNotFoundException           => new ProblemDetails { Title = "Recipe not found",           Status = 404, Detail = ex.Message },
InvalidStatusTransitionException  => new ProblemDetails { Title = "Invalid status transition",   Status = 422, Detail = ex.Message },
IngredientNotOwnedException       => new ProblemDetails { Title = "Ingredient not found",        Status = 404, Detail = ex.Message },
```

---

## Step 6 — Generate the Migration

```bash
dotnet ef migrations add AddRecipes --project src/Nastart.Api
dotnet ef database update --project src/Nastart.Api
```

---

## ✅ Test It

```http
### Create a recipe
POST http://localhost:5000/recipes
Authorization: Bearer {{token}}
Content-Type: application/json

{
  "name": "Sourdough Loaf",
  "sellingPrice": 8.00,
  "ingredients": [
    { "ingredientId": "{{flourId}}", "quantity": 0.5, "unit": "kg" },
    { "ingredientId": "{{saltId}}", "quantity": 0.01, "unit": "kg" }
  ]
}

### Activate the recipe (Draft → Active)
PATCH http://localhost:5000/recipes/{{recipeId}}/status
Authorization: Bearer {{token}}
Content-Type: application/json

{ "status": "Active" }

### Try an invalid transition (Active → Draft)
PATCH http://localhost:5000/recipes/{{recipeId}}/status
Authorization: Bearer {{token}}
Content-Type: application/json

{ "status": "Draft" }
```

Expected: `201 Created` on create, `200 OK` on valid transition, `422 Unprocessable Entity` on invalid transition.

---

## Key Concepts

| Term | Definition |
|---|---|
| **Junction table** | A table that links two other tables in a many-to-many relationship. Each row is one link. |
| **`ON DELETE CASCADE`** | Removing a recipe removes all its `recipe_ingredients` rows automatically. |
| **`ON DELETE RESTRICT`** | Prevents deleting an ingredient that is referenced by a recipe. |
| **`HasConversion<string>()`** | Stores an enum as its string name (`"Draft"`) rather than its integer value (`0`). Safer for migrations and readable in SQL. |
| **State machine** | A pattern where an entity can only be in one state at a time, and transitions between states are explicitly controlled. |
| **Aggregate validation** | Loading all referenced IDs in one `WHERE id IN (...)` query before writing — prevents partial saves. |

---

## Next

Lesson 25 → **Recipe Costing** — `GET /recipes/{id}/cost` calculates the total ingredient cost from current prices and updates `last_cost` and `margin_percent` on the recipe.
