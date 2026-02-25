# Lesson 02: Your First Minimal API Endpoint

## 🎯 What you'll build
A `GET /ingredients` endpoint that returns a hardcoded JSON list — your first working API response.

---

## 🔧 The .NET Way

### MapGet, MapPost, MapPut, MapDelete

In Minimal APIs, you register routes directly on the `app` object:

```csharp
app.MapGet("/path", handler);
app.MapPost("/path", handler);
app.MapPut("/path/{id}", handler);
app.MapDelete("/path/{id}", handler);
```

Compare:
```python
# FastAPI equivalent
@app.get("/ingredients")
def get_ingredients(): ...
```
```javascript
// Express equivalent
app.get('/ingredients', (req, res) => { ... });
```

### Return types

Minimal API handlers can return:

| Return | What the client gets | C# example |
|--------|---------------------|------------|
| A C# object | 200 JSON | `() => new { Name = "Flour" }` |
| `TypedResults.Ok(x)` | 200 JSON (explicit) | `TypedResults.Ok(ingredients)` |
| `TypedResults.NotFound()` | 404 empty | `TypedResults.NotFound()` |
| `TypedResults.Created(url, x)` | 201 JSON | `TypedResults.Created(...)` |
| `TypedResults.NoContent()` | 204 empty | `TypedResults.NoContent()` |

**Always use `TypedResults` (not `Results`)** — it lets OpenAPI know the exact status codes your endpoint can return, so the docs are accurate.

### Route parameters

```csharp
// {id} is a route segment — captured as a parameter
app.MapGet("/ingredients/{id}", (Guid id) => { ... });
//                                  ↑ .NET automatically parses the string to Guid
```

Compare:
```python
# FastAPI
@app.get("/ingredients/{id}")
def get(id: uuid.UUID): ...
```

### Query string parameters

```csharp
// ?name=flour → name parameter
app.MapGet("/ingredients", (string? name) => { ... });
//                              ↑ ? means optional. If not provided, name = null
```

---

## Step 1 — Define the Ingredient record

C# **records** are immutable data types — similar to Python `@dataclass(frozen=True)` or a TypeScript `interface`.

Create `src/Nastart.Api/Features/Ingredients/Ingredient.cs`:

```csharp
namespace Nastart.Api.Features.Ingredients;
// ↑ Namespace = like a Python module path. Convention: Company.Project.FolderPath

/// <summary>
/// Represents one ingredient (e.g., Flour, Sugar).
/// </summary>
public class Ingredient
{
    // Guid = globally unique identifier, like Python's uuid.UUID
    public Guid Id { get; set; }

    // required = the compiler will warn if you create this without Name
    public required string Name { get; set; }

    // string? = nullable string. Without ?, the string can never be null.
    public string? Unit { get; set; }          // e.g., "kg", "g", "pcs"

    // decimal = for money/measurements. float/double lose precision.
    public decimal CurrentPricePerUnit { get; set; }

    // DateTimeOffset = date+time with timezone. Always use this over DateTime.
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

> **C# vs Python/JS**
> - `string` (not nullable) ≈ Python `str` with a runtime check — the compiler enforces it
> - `string?` ≈ Python `Optional[str]` or TS `string | null`
> - `required` ≈ Python `@dataclass` with no default, or Zod's `.required()`
> - `{ get; set; }` = auto-property. Creates a private backing field automatically.

---

## Step 2 — Register a route group

Route groups let you prefix all ingredient routes with `/ingredients` without repeating yourself.

Create `src/Nastart.Api/Features/Ingredients/IngredientsEndpoints.cs`:

```csharp
namespace Nastart.Api.Features.Ingredients;

public static class IngredientsEndpoints
{
    // Extension method on WebApplication — called from Program.cs
    // 'this WebApplication app' means: add this method to the app object
    public static WebApplication MapIngredientsEndpoints(this WebApplication app)
    {
        // All routes below will be prefixed with /ingredients
        var group = app.MapGroup("/ingredients")
            .WithTags("Ingredients");    // groups endpoints in the Scalar/Swagger UI

        group.MapGet("/", GetAll);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);

        return app;
    }

    // ── Handlers (static methods) ────────────────────────────────────

    static IResult GetAll()
    {
        // Hardcoded for now — Lesson 04 replaces this with a real database
        var ingredients = new List<Ingredient>
        {
            new() { Id = Guid.NewGuid(), Name = "Flour",  Unit = "kg",  CurrentPricePerUnit = 12_000 },
            new() { Id = Guid.NewGuid(), Name = "Sugar",  Unit = "kg",  CurrentPricePerUnit = 16_000 },
            new() { Id = Guid.NewGuid(), Name = "Butter", Unit = "kg",  CurrentPricePerUnit = 90_000 },
        };

        return TypedResults.Ok(ingredients);
    }

    static IResult GetById(Guid id)
    {
        // Pretend lookup — replaced in Lesson 04
        var ingredient = new Ingredient
        {
            Id = id,
            Name = "Flour",
            Unit = "kg",
            CurrentPricePerUnit = 12_000
        };

        // ternary: condition ? value_if_true : value_if_false
        return ingredient is not null
            ? TypedResults.Ok(ingredient)
            : TypedResults.NotFound();
    }

    static IResult Create(Ingredient ingredient)
    {
        // Bind request body automatically — .NET deserializes JSON to the Ingredient type
        ingredient.Id = Guid.NewGuid();
        ingredient.CreatedAt = DateTimeOffset.UtcNow;

        // 201 Created + Location header pointing to the new resource
        return TypedResults.Created($"/ingredients/{ingredient.Id}", ingredient);
    }

    static IResult Update(Guid id, Ingredient updated)
    {
        // 204 No Content = success, nothing to return
        return TypedResults.NoContent();
    }

    static IResult Delete(Guid id)
    {
        return TypedResults.NoContent();
    }
}
```

---

## Step 3 — Register the routes in Program.cs

```csharp
using Scalar.AspNetCore;
using Nastart.Api.Features.Ingredients;    // ← add this using

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapGet("/", () => "Nastart API is running!");

// Register ingredient routes
app.MapIngredientsEndpoints();    // ← calls the extension method

app.Run();
```

---

## ✅ Run it

```powershell
dotnet run
```

Test with curl or the Scalar UI:

```powershell
# Get all ingredients
curl https://localhost:7xxx/ingredients

# Create one
curl -X POST https://localhost:7xxx/ingredients `
  -H "Content-Type: application/json" `
  -d '{"name": "Eggs", "unit": "pcs", "currentPricePerUnit": 2000}'

# Get by id
curl https://localhost:7xxx/ingredients/123e4567-e89b-12d3-a456-426614174000
```

---

## 🔑 Key Concepts

| Concept | What it is |
|---------|-----------|
| `MapGet/Post/Put/Delete` | Register a route handler |
| `MapGroup` | Prefix routes, avoid repetition |
| `TypedResults` | Return strongly-typed HTTP responses |
| `IResult` | The return type for any HTTP response |
| C# `record` / `class` | Data types — `class` is mutable, `record` adds value equality |
| `{ get; set; }` | Auto-property — shorthand for a field with getter/setter |
| Extension method | Add a method to an existing type — `this WebApplication app` on the first parameter |
| `Guid` | UUID type — use for all entity IDs |
| `decimal` | Exact numeric type — always use for money, never `float` |
| `string?` | Nullable string — `?` allows null |

---

➡️ Next: [Lesson 03 — Full CRUD with in-memory storage](03-crud-in-memory.md)
