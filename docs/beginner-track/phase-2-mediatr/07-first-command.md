# Lesson 07: Your First Command and Query

## 🎯 What you'll build
Convert two endpoints — `GET /ingredients` (query) and `POST /ingredients` (command) — to use MediatR. The endpoint bodies shrink to a single `mediator.Send(...)` line.

---

## 🔧 The Pattern

Every feature slice will follow this exact shape:

```
Features/Ingredients/
├── GetIngredients.cs       ← Query + Handler (one file per feature)
├── CreateIngredient.cs     ← Command + Handler
├── Ingredient.cs
├── IngredientRequests.cs
└── IngredientsEndpoints.cs
```

**One file per feature.** The query/command record and its handler live together.

---

## Step 1 — GetIngredients (Query)

Create `src/Nastart.Api/Features/Ingredients/GetIngredients.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Ingredients;

// ── Query ──────────────────────────────────────────────────────────

/// <summary>
/// Fetches all ingredients. A Query = read-only, no side effects.
/// </summary>
public record GetIngredientsQuery : IRequest<List<Ingredient>>;
// ↑ record with no properties = a signal with no parameters
// Compare Python: class GetIngredientsQuery: pass  (just a marker type)

// ── Handler ────────────────────────────────────────────────────────

/// <summary>
/// Handles GetIngredientsQuery → returns all ingredients from the database.
/// </summary>
public class GetIngredientsHandler(NastartDbContext db)
    : IRequestHandler<GetIngredientsQuery, List<Ingredient>>
//           ↑ "I handle GetIngredientsQuery and return List<Ingredient>"
{
    // Primary constructor injects NastartDbContext — same as before
    // 'db' is available as a field automatically in C# 12 primary constructors

    public async Task<List<Ingredient>> Handle(
        GetIngredientsQuery request,
        CancellationToken cancellationToken)
    //                     ↑ CancellationToken = lets the request be cancelled
    //                       (e.g., user closes browser). Pass it to all async calls.
    {
        return await db.Ingredients.ToListAsync(cancellationToken);
    }
}
```

---

## Step 2 — GetIngredientById (Query with parameter)

Add to the same file (or create `GetIngredientById.cs`):

```csharp
// ── Query ──────────────────────────────────────────────────────────

public record GetIngredientByIdQuery(Guid Id) : IRequest<Ingredient?>;
//                                       ↑ positional parameter — Id is both a property and constructor param

// ── Handler ────────────────────────────────────────────────────────

public class GetIngredientByIdHandler(NastartDbContext db)
    : IRequestHandler<GetIngredientByIdQuery, Ingredient?>
{
    public async Task<Ingredient?> Handle(
        GetIngredientByIdQuery request,
        CancellationToken cancellationToken)
    {
        // FindAsync doesn't take a CancellationToken — use FirstOrDefaultAsync instead
        return await db.Ingredients
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
    }
}
```

---

## Step 3 — CreateIngredient (Command)

Create `src/Nastart.Api/Features/Ingredients/CreateIngredient.cs`:

```csharp
using FluentValidation;
using MediatR;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Ingredients;

// ── Command ────────────────────────────────────────────────────────

/// <summary>
/// Creates a new ingredient. A Command = changes state, can have side effects.
/// </summary>
public record CreateIngredientCommand(
    string Name,
    string? Unit,
    decimal CurrentPricePerUnit
) : IRequest<Ingredient>;

// ── Validator ──────────────────────────────────────────────────────

/// <summary>
/// Validates the command. Note: validator is on the COMMAND, not the HTTP request.
/// This means it works regardless of where the command is sent from (API, bot, test).
/// </summary>
public class CreateIngredientCommandValidator : AbstractValidator<CreateIngredientCommand>
{
    public CreateIngredientCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CurrentPricePerUnit).GreaterThan(0);
        RuleFor(x => x.Unit).MaximumLength(50).When(x => x.Unit is not null);
    }
}

// ── Handler ────────────────────────────────────────────────────────

public class CreateIngredientHandler(NastartDbContext db)
    : IRequestHandler<CreateIngredientCommand, Ingredient>
{
    public async Task<Ingredient> Handle(
        CreateIngredientCommand request,
        CancellationToken cancellationToken)
    {
        var ingredient = new Ingredient
        {
            Name = request.Name,
            Unit = request.Unit,
            CurrentPricePerUnit = request.CurrentPricePerUnit,
        };

        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync(cancellationToken);

        return ingredient;
    }
}
```

> **Notice**: The validator moved from `CreateIngredientRequest` → `CreateIngredientCommand`.  
> Now validation happens at the command level, not the HTTP level. The bot (Phase 3) gets the same validation for free.

---

## Step 4 — Update the endpoints to send commands

Update `IngredientsEndpoints.cs`:

```csharp
using MediatR;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Ingredients;

public static class IngredientsEndpoints
{
    public static WebApplication MapIngredientsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/ingredients").WithTags("Ingredients");

        group.MapGet("/",             GetAll);
        group.MapGet("/{id:guid}",    GetById);
        group.MapPost("/",            Create);
        group.MapPut("/{id:guid}",    Update);
        group.MapDelete("/{id:guid}", Delete);

        return app;
    }

    // ── The endpoint body is now just one line ────────────────────

    static async Task<IResult> GetAll(IMediator mediator)
    {
        var ingredients = await mediator.Send(new GetIngredientsQuery());
        return TypedResults.Ok(ingredients);
    }

    static async Task<IResult> GetById(Guid id, IMediator mediator)
    {
        var ingredient = await mediator.Send(new GetIngredientByIdQuery(id));
        return ingredient is not null ? TypedResults.Ok(ingredient) : TypedResults.NotFound();
    }

    static async Task<IResult> Create(CreateIngredientRequest request, IMediator mediator)
    {
        // Map HTTP DTO → MediatR command
        var command = new CreateIngredientCommand(
            request.Name,
            request.Unit,
            request.CurrentPricePerUnit
        );

        var ingredient = await mediator.Send(command);
        return TypedResults.Created($"/ingredients/{ingredient.Id}", ingredient);
    }

    // ── Update + Delete still use db directly for now ────────────
    // (refactored in Lesson 09)

    static async Task<IResult> Update(
        Guid id,
        UpdateIngredientRequest request,
        NastartDbContext db)
    {
        var ingredient = await db.Ingredients.FindAsync(id);
        if (ingredient is null) return TypedResults.NotFound();

        ingredient.Name = request.Name ?? ingredient.Name;
        ingredient.Unit = request.Unit ?? ingredient.Unit;
        ingredient.CurrentPricePerUnit = request.CurrentPricePerUnit ?? ingredient.CurrentPricePerUnit;

        await db.SaveChangesAsync();
        return TypedResults.Ok(ingredient);
    }

    static async Task<IResult> Delete(Guid id, NastartDbContext db)
    {
        var ingredient = await db.Ingredients.FindAsync(id);
        if (ingredient is null) return TypedResults.NotFound();

        db.Ingredients.Remove(ingredient);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }
}
```

---

## ✅ Run it

```powershell
dotnet run
```

The endpoints still work exactly the same from the outside. But now `GET /ingredients` and `POST /ingredients` are one `mediator.Send()` call — the actual logic lives in the handler.

Try the Scalar UI — all endpoints still function.

---

## 🔑 Key Concepts

| Concept | What it is |
|---------|-----------|
| `IRequest<T>` | A message type — put command/query parameters in the record properties |
| `IRequestHandler<TReq, TRes>` | Processes one IRequest type, returns `TRes` |
| Query vs Command | Query = read (no side effects) / Command = write (changes state) |
| `mediator.Send()` | Dispatch — MediatR finds the right handler automatically |
| `CancellationToken` | Propagates request cancellation through async chains — always pass it |
| One handler per request | MediatR enforces one handler per `IRequest<T>` type |
| Validator on the command | Validation lives at the command level → works from any caller |

---

➡️ Next: [Lesson 08 — Pipeline Behaviors](08-pipeline-behaviors.md)
