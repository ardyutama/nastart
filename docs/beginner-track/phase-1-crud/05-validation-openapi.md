# Lesson 05: Validation + OpenAPI Docs

## 🎯 What you'll build
Add input validation with FluentValidation so bad requests get clear error messages, and enrich the OpenAPI docs so every endpoint shows its request shape, response types, and examples.

---

## 🔧 The .NET Way

### The problem without validation

Right now, someone can `POST /ingredients` with an empty name and it saves to the database. We need to reject bad input before it gets anywhere near the database.

### FluentValidation

FluentValidation is the standard .NET validation library. You define rules as a class, not as attributes on the model.

Compare:
```python
# Python Pydantic
class CreateIngredientRequest(BaseModel):
    name: str = Field(min_length=1, max_length=200)
    price: Decimal = Field(gt=0)
```
```csharp
// FluentValidation (C#)
public class CreateIngredientValidator : AbstractValidator<CreateIngredientRequest>
{
    public CreateIngredientValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CurrentPricePerUnit).GreaterThan(0);
    }
}
```

FluentValidation is kept separate from the model — cleaner than Pydantic's combined approach, easier to test validators independently.

### ProblemDetails

`ProblemDetails` is the RFC 7807 standard format for error responses. .NET has built-in support for it.

```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Validation failed",
  "status": 400,
  "errors": {
    "Name": ["'Name' must not be empty."],
    "CurrentPricePerUnit": ["'CurrentPricePerUnit' must be greater than 0."]
  }
}
```

This is what APIs that follow standards return instead of `"error": "something went wrong"`.

---

## Step 1 — Install FluentValidation

```powershell
dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions
```

---

## Step 2 — Create validators

Create `src/Nastart.Api/Features/Ingredients/IngredientValidators.cs`:

```csharp
using FluentValidation;

namespace Nastart.Api.Features.Ingredients;

/// <summary>
/// Validates the create request.
/// AbstractValidator<T> is the base class for all FluentValidation validators.
/// </summary>
public class CreateIngredientValidator : AbstractValidator<CreateIngredientRequest>
{
    public CreateIngredientValidator()
    {
        // RuleFor() selects a property. Chain validation rules after it.
        RuleFor(x => x.Name)
            .NotEmpty()                 // not null and not whitespace
            .MaximumLength(200)
            .WithMessage("Name must be between 1 and 200 characters.");

        RuleFor(x => x.CurrentPricePerUnit)
            .GreaterThan(0)
            .WithMessage("Price must be greater than zero.");

        RuleFor(x => x.Unit)
            .MaximumLength(50)
            .When(x => x.Unit is not null);   // only validate Unit if it was provided
    }
}

public class UpdateIngredientValidator : AbstractValidator<UpdateIngredientRequest>
{
    public UpdateIngredientValidator()
    {
        // Optional field validation — only run if a value was provided
        When(x => x.Name is not null, () =>
        {
            RuleFor(x => x.Name!).NotEmpty().MaximumLength(200);
        });

        When(x => x.CurrentPricePerUnit is not null, () =>
        {
            RuleFor(x => x.CurrentPricePerUnit!.Value).GreaterThan(0);
        });

        When(x => x.Unit is not null, () =>
        {
            RuleFor(x => x.Unit!).MaximumLength(50);
        });
    }
}
```

---

## Step 3 — Add a validation helper

Create `src/Nastart.Api/Shared/Validation/ValidationHelper.cs`:

```csharp
using FluentValidation;

namespace Nastart.Api.Shared.Validation;

/// <summary>
/// Runs a FluentValidation validator and returns a ProblemDetails result on failure.
/// </summary>
public static class ValidationHelper
{
    /// <summary>
    /// Validates <paramref name="request"/> using <paramref name="validator"/>.
    /// Returns null if valid, or an IResult with 400 + error details if invalid.
    /// </summary>
    public static async Task<IResult?> ValidateAsync<T>(
        IValidator<T> validator,
        T request)
    {
        var result = await validator.ValidateAsync(request);

        if (result.IsValid) return null;    // null = "no error, proceed"

        // Build a dictionary of field → error messages
        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );

        // TypedResults.ValidationProblem = 400 with RFC 7807 ProblemDetails body
        return TypedResults.ValidationProblem(errors);
    }
}
```

---

## Step 4 — Register validators + wire into endpoints

Update `Program.cs` to register validators:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Shared.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

builder.Services.AddDbContext<NastartDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);

// Register all validators in the assembly automatically
// ↓ This scans for classes that inherit AbstractValidator<T>
builder.Services.AddValidatorsFromAssemblyContaining<CreateIngredientValidator>();

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

Update the POST and PUT handlers in `IngredientsEndpoints.cs` to use validation:

```csharp
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Validation;

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

    static async Task<IResult> GetAll(NastartDbContext db)
    {
        var ingredients = await db.Ingredients.ToListAsync();
        return TypedResults.Ok(ingredients);
    }

    static async Task<IResult> GetById(Guid id, NastartDbContext db)
    {
        var ingredient = await db.Ingredients.FindAsync(id);
        return ingredient is not null ? TypedResults.Ok(ingredient) : TypedResults.NotFound();
    }

    // IValidator<CreateIngredientRequest> validator ← injected from DI (registered above)
    static async Task<IResult> Create(
        CreateIngredientRequest request,
        IValidator<CreateIngredientRequest> validator,
        NastartDbContext db)
    {
        // Run validation — returns 400 ProblemDetails if invalid, null if ok
        var validationError = await ValidationHelper.ValidateAsync(validator, request);
        if (validationError is not null) return validationError;

        var ingredient = new Ingredient
        {
            Name = request.Name,
            Unit = request.Unit,
            CurrentPricePerUnit = request.CurrentPricePerUnit,
        };

        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/ingredients/{ingredient.Id}", ingredient);
    }

    static async Task<IResult> Update(
        Guid id,
        UpdateIngredientRequest request,
        IValidator<UpdateIngredientRequest> validator,
        NastartDbContext db)
    {
        var validationError = await ValidationHelper.ValidateAsync(validator, request);
        if (validationError is not null) return validationError;

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

## Step 5 — Enrich OpenAPI documentation

Add metadata to endpoints so the Scalar UI shows what each one does:

```csharp
// In IngredientsEndpoints.cs, update the route registration:

group.MapGet("/", GetAll)
    .WithSummary("List all ingredients")
    .WithDescription("Returns the full ingredient catalogue with current prices.");

group.MapGet("/{id:guid}", GetById)
    .WithSummary("Get ingredient by ID")
    .Produces<Ingredient>(200)
    .Produces(404);

group.MapPost("/", Create)
    .WithSummary("Create a new ingredient")
    .Produces<Ingredient>(201)
    .ProducesValidationProblem();

group.MapPut("/{id:guid}", Update)
    .WithSummary("Update an ingredient")
    .Produces<Ingredient>(200)
    .Produces(404)
    .ProducesValidationProblem();

group.MapDelete("/{id:guid}", Delete)
    .WithSummary("Delete an ingredient")
    .Produces(204)
    .Produces(404);
```

---

## ✅ Run it and test validation

```powershell
dotnet run
```

**Test: empty name → 400 error**
```powershell
curl -X POST https://localhost:7xxx/ingredients `
  -H "Content-Type: application/json" `
  -d '{"name": "", "currentPricePerUnit": 12000}'
# Returns: 400 with { "errors": { "Name": ["Name must be between 1 and 200 characters."] } }
```

**Test: negative price → 400 error**
```powershell
curl -X POST https://localhost:7xxx/ingredients `
  -H "Content-Type: application/json" `
  -d '{"name": "Flour", "currentPricePerUnit": -100}'
# Returns: 400 with { "errors": { "CurrentPricePerUnit": ["Price must be greater than zero."] } }
```

Open `/scalar/v1` — each endpoint now shows its expected responses.

---

## 🔑 Key Concepts

| Concept | What it is |
|---------|-----------|
| FluentValidation | Library for writing validation rules as classes |
| `AbstractValidator<T>` | Base class for a validator — one per request type |
| `RuleFor(x => x.Prop)` | Target a property for validation |
| `NotEmpty()` / `MaximumLength()` etc. | Chainable validation rules |
| `When()` | Conditional rule — only validate if a condition is true |
| ProblemDetails / RFC 7807 | Standard error response format — keeps APIs consistent |
| `TypedResults.ValidationProblem()` | Returns 400 with ProblemDetails body |
| `AddValidatorsFromAssemblyContaining<T>` | Auto-register all validators in the project |
| `.WithSummary()` `.Produces<T>()` | OpenAPI metadata — tells docs what the endpoint does |

---

> **Phase 1 complete.** You have a fully working CRUD API with a real database, input validation, and interactive documentation. The code works, but every handler directly talks to the database — it's tightly coupled.
>
> Phase 2 fixes that.

---

➡️ Next: [Lesson 06 — What is MediatR and why?](../phase-2-mediatr/06-what-is-mediatr.md)
