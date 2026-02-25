# Lesson 06: What is MediatR and Why?

## 🎯 What you'll build
Understand the problem MediatR solves, install it, and register it — ready for Lessons 07–09 where you refactor the endpoints.

---

## 🔧 The Problem: Tight Coupling

Look at the current `Create` handler:

```csharp
static async Task<IResult> Create(
    CreateIngredientRequest request,
    IValidator<CreateIngredientRequest> validator,
    NastartDbContext db)
{
    var validationError = await ValidationHelper.ValidateAsync(validator, request);
    if (validationError is not null) return validationError;

    var ingredient = new Ingredient { ... };
    db.Ingredients.Add(ingredient);
    await db.SaveChangesAsync();

    return TypedResults.Created($"/ingredients/{ingredient.Id}", ingredient);
}
```

This handler:
- Validates the request
- Creates the entity
- Saves to the database
- Returns the HTTP result

**All in one place.** That's a problem when you need the same "create ingredient" logic called from:
1. The REST endpoint
2. A Telegram bot command
3. An import script
4. A test

You'd have to either duplicate the logic or make all callers depend on the endpoint directly. Neither is good.

---

## 🔧 The Solution: The Mediator Pattern

The Mediator pattern introduces a **single, dumb message bus** between callers and handlers:

```
Before:
  Endpoint ──────────────────────► Database (direct)

After (with MediatR):
  Endpoint ──► Mediator ──► Handler ──► Database
  Bot      ──► Mediator ──► Handler (same handler!)
  Script   ──► Mediator ──► Handler (same handler!)
```

The endpoint just says: *"here's a `CreateIngredientCommand` — someone handle it."*  
The handler says: *"I handle `CreateIngredientCommand`."*

They never know about each other. You can add 10 new callers without touching the handler.

---

## 🔧 MediatR's Two Main Concepts

### IRequest<TResponse>

A message (command or query). You send it into the mediator. Compare to Python's dataclass events or a JS Redux action.

```csharp
// A query — asks for data, no side effects
public record GetIngredientsQuery : IRequest<List<Ingredient>>;

// A command — changes state, returns something
public record CreateIngredientCommand(string Name, string? Unit, decimal Price)
    : IRequest<Ingredient>;
```

### IRequestHandler<TRequest, TResponse>

The handler — the thing that processes one specific message type.

```csharp
public class GetIngredientsHandler
    : IRequestHandler<GetIngredientsQuery, List<Ingredient>>
{
    private readonly NastartDbContext _db;

    public GetIngredientsHandler(NastartDbContext db)
    {
        _db = db;
    }

    public async Task<List<Ingredient>> Handle(
        GetIngredientsQuery request,
        CancellationToken cancellationToken)
    {
        return await _db.Ingredients.ToListAsync(cancellationToken);
    }
}
```

### Sending a request

From anywhere (endpoint, bot, test):

```csharp
// inject IMediator
var ingredients = await mediator.Send(new GetIngredientsQuery());
var created     = await mediator.Send(new CreateIngredientCommand("Flour", "kg", 12000));
```

---

## Step 1 — Install MediatR

```powershell
dotnet add package MediatR
```

---

## Step 2 — Register MediatR in Program.cs

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Shared.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

builder.Services.AddDbContext<NastartDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);

builder.Services.AddValidatorsFromAssemblyContaining<CreateIngredientValidator>();

// Register MediatR — scans the assembly for all IRequestHandler<> implementations
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<Program>()
);
// ↑ AssemblyContaining<Program> = "scan the assembly where Program.cs lives"
// Compare: Python's autodiscover, or how Django finds views

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapGet("/", () => "Nastart API is running!");
app.MapIngredientsEndpoints();

app.Run();
```

---

## ✅ Verify it compiles

```powershell
dotnet build
```

No errors. MediatR is registered but nothing uses it yet — that changes in Lesson 07.

---

## 🔑 Key Concepts

| Concept | What it is |
|---------|-----------|
| Mediator pattern | A bus between senders and handlers — they don't know each other |
| `IRequest<TResponse>` | A message type — command (change state) or query (fetch data) |
| `IRequestHandler<TReq, TRes>` | The class that processes one specific message type |
| `IMediator.Send()` | Dispatch a message to its handler |
| Tight coupling | When code directly depends on specific implementations — hard to reuse/test |
| Loose coupling | When code depends on abstractions (interfaces/messages) — easy to reuse/test |
| `RegisterServicesFromAssemblyContaining<T>` | Auto-discovery of all handlers in the project |

---

> **The workflow in Lessons 07–09**:  
> For each endpoint: extract the logic into a `IRequest` + `IRequestHandler`, then replace the endpoint body with a single `mediator.Send(...)` call.

---

➡️ Next: [Lesson 07 — Your first Command and Query](07-first-command.md)
