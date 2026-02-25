# Lesson 09: Refactor All Features

## 🎯 What you'll build
Convert the remaining three endpoints — `PUT /ingredients/{id}`, `DELETE /ingredients/{id}`, and a new `UpdateIngredientPrice` command — to MediatR. End the lesson with a fully clean `Program.cs` and endpoint files.

---

## Step 1 — UpdateIngredient Command + Handler

Create `src/Nastart.Api/Features/Ingredients/UpdateIngredient.cs`:

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Ingredients;

// ── Command ────────────────────────────────────────────────────────

public record UpdateIngredientCommand(
    Guid Id,
    string? Name,
    string? Unit,
    decimal? CurrentPricePerUnit
) : IRequest<Ingredient?>;
// Returns Ingredient? — null means "not found"

// ── Validator ──────────────────────────────────────────────────────

public class UpdateIngredientCommandValidator : AbstractValidator<UpdateIngredientCommand>
{
    public UpdateIngredientCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        When(x => x.Name is not null, () =>
            RuleFor(x => x.Name!).NotEmpty().MaximumLength(200)
        );
        When(x => x.CurrentPricePerUnit is not null, () =>
            RuleFor(x => x.CurrentPricePerUnit!.Value).GreaterThan(0)
        );
        When(x => x.Unit is not null, () =>
            RuleFor(x => x.Unit!).MaximumLength(50)
        );
    }
}

// ── Handler ────────────────────────────────────────────────────────

public class UpdateIngredientHandler(NastartDbContext db)
    : IRequestHandler<UpdateIngredientCommand, Ingredient?>
{
    public async Task<Ingredient?> Handle(
        UpdateIngredientCommand request,
        CancellationToken cancellationToken)
    {
        var ingredient = await db.Ingredients
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (ingredient is null) return null;    // ← not found, endpoint returns 404

        ingredient.Name = request.Name ?? ingredient.Name;
        ingredient.Unit = request.Unit ?? ingredient.Unit;
        ingredient.CurrentPricePerUnit = request.CurrentPricePerUnit ?? ingredient.CurrentPricePerUnit;

        await db.SaveChangesAsync(cancellationToken);
        return ingredient;
    }
}
```

---

## Step 2 — DeleteIngredient Command + Handler

Create `src/Nastart.Api/Features/Ingredients/DeleteIngredient.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Ingredients;

// ── Command ────────────────────────────────────────────────────────

/// <summary>
/// Deletes an ingredient. Returns true if deleted, false if not found.
/// </summary>
public record DeleteIngredientCommand(Guid Id) : IRequest<bool>;

// ── Handler ────────────────────────────────────────────────────────

public class DeleteIngredientHandler(NastartDbContext db)
    : IRequestHandler<DeleteIngredientCommand, bool>
{
    public async Task<bool> Handle(
        DeleteIngredientCommand request,
        CancellationToken cancellationToken)
    {
        var ingredient = await db.Ingredients
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (ingredient is null) return false;

        db.Ingredients.Remove(ingredient);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
```

---

## Step 3 — Final IngredientsEndpoints.cs

Replace the entire file with the fully refactored version — every handler is now one `mediator.Send()` line:

```csharp
using MediatR;

namespace Nastart.Api.Features.Ingredients;

public static class IngredientsEndpoints
{
    public static WebApplication MapIngredientsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/ingredients").WithTags("Ingredients");

        group.MapGet("/",             GetAll)        .WithSummary("List all ingredients");
        group.MapGet("/{id:guid}",    GetById)       .WithSummary("Get ingredient by ID");
        group.MapPost("/",            Create)        .WithSummary("Create ingredient");
        group.MapPut("/{id:guid}",    Update)        .WithSummary("Update ingredient");
        group.MapDelete("/{id:guid}", Delete)        .WithSummary("Delete ingredient");

        return app;
    }

    // Every handler is now a thin HTTP adapter.
    // All business logic lives in the MediatR handlers.

    static async Task<IResult> GetAll(IMediator mediator)
        => TypedResults.Ok(await mediator.Send(new GetIngredientsQuery()));

    static async Task<IResult> GetById(Guid id, IMediator mediator)
    {
        var result = await mediator.Send(new GetIngredientByIdQuery(id));
        return result is not null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    static async Task<IResult> Create(CreateIngredientRequest request, IMediator mediator)
    {
        var ingredient = await mediator.Send(
            new CreateIngredientCommand(request.Name, request.Unit, request.CurrentPricePerUnit));

        return TypedResults.Created($"/ingredients/{ingredient.Id}", ingredient);
    }

    static async Task<IResult> Update(Guid id, UpdateIngredientRequest request, IMediator mediator)
    {
        var result = await mediator.Send(
            new UpdateIngredientCommand(id, request.Name, request.Unit, request.CurrentPricePerUnit));

        return result is not null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    static async Task<IResult> Delete(Guid id, IMediator mediator)
    {
        var deleted = await mediator.Send(new DeleteIngredientCommand(id));
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
```

> **Notice** what happened to each endpoint: they went from 10+ lines with database+validation logic down to 1–2 lines. The endpoint is now just an HTTP adapter — it translates HTTP → MediatR command, and MediatR result → HTTP response.

---

## Step 4 — The final Program.cs

Here is the clean, final `Program.cs`. Everything is registered, nothing leaks business logic:

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Shared.Behaviors;
using Nastart.Api.Shared.Data;

var builder = WebApplication.CreateBuilder(args);

// ── Infrastructure ─────────────────────────────────────────────────

builder.Services.AddOpenApi();

builder.Services.AddDbContext<NastartDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

// ── App ────────────────────────────────────────────────────────────

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

// Global exception handler — ValidationException → 400, everything else → 500
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var feature = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

        if (feature?.Error is FluentValidation.ValidationException validationEx)
        {
            var errors = validationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Validation failed",
                status = 400,
                errors
            });
            return;
        }

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = "An unexpected error occurred",
            status = 500
        });
    });
});

app.MapGet("/", () => "Nastart API is running!");
app.MapIngredientsEndpoints();

app.Run();
```

---

## ✅ Run it — final check

```powershell
dotnet run
```

Run through the full CRUD cycle using the Scalar UI or curl. Everything works exactly as before from the outside, but the code is now cleanly separated:

- `IngredientsEndpoints.cs` — HTTP adapters only
- `GetIngredients.cs`, `CreateIngredient.cs`, etc. — business logic only
- `ValidationBehavior.cs` — validation for all commands automatically
- `LoggingBehavior.cs` — logging for all commands automatically

---

## What the project looks like now

```
src/Nastart.Api/
├── Features/
│   └── Ingredients/
│       ├── Ingredient.cs
│       ├── IngredientRequests.cs
│       ├── IngredientsEndpoints.cs     ← HTTP adapters only
│       ├── GetIngredients.cs           ← Query + Handler
│       ├── CreateIngredient.cs         ← Command + Validator + Handler
│       ├── UpdateIngredient.cs         ← Command + Validator + Handler
│       └── DeleteIngredient.cs         ← Command + Handler
├── Shared/
│   ├── Behaviors/
│   │   ├── ValidationBehavior.cs
│   │   └── LoggingBehavior.cs
│   └── Data/
│       └── NastartDbContext.cs
└── Program.cs
```

---

## 🔑 Key Concepts (Phase 2 summary)

| Before MediatR | After MediatR |
|----------------|---------------|
| Endpoint has database + validation + business logic | Endpoint has 1 line: `mediator.Send(...)` |
| Adding a new caller (bot, script) duplicates logic | New caller just sends the same command |
| Validation must be manually called | Validation runs automatically via `ValidationBehavior` |
| Logging requires each handler to log separately | Logging runs automatically via `LoggingBehavior` |

---

> **Phase 2 complete.** The API is now cleanly separated. Phase 3 adds a Telegram bot that reuses these exact same MediatR commands — no duplication.

---

➡️ Next: [Lesson 10 — Create your Telegram bot](../phase-3-telegram/10-create-your-bot.md)
