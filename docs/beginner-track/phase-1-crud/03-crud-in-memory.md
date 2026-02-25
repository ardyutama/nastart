# Lesson 03: Full CRUD with In-Memory Storage

## 🎯 What you'll build
A working CRUD API backed by an in-memory `List<Ingredient>` — no database yet. Create, read, update, and delete ingredients with real data persistence per session.

---

## 🔧 The .NET Way

### Static state for in-memory storage

For quick prototyping, a `static` field on a class acts like a module-level variable in Python:

```csharp
// C#
private static readonly List<Ingredient> _store = [];

// Python equivalent
_store: list[Ingredient] = []
```

`readonly` means the reference can't be reassigned (you can't do `_store = new List<>()`), but the list contents can still change.

### C# generics

`List<Ingredient>` means a list that only holds `Ingredient` objects.  
Compare: Python `list[Ingredient]` (type hint only), TypeScript `Ingredient[]`.

### Pattern matching with `is not null`

```csharp
var found = _store.FirstOrDefault(x => x.Id == id);

if (found is null)
    return TypedResults.NotFound();

// Here the compiler knows found is not null — no null reference risk
return TypedResults.Ok(found);
```

This is safer than `if (found != null)` because the compiler's null-flow analysis tracks it.

### LINQ

LINQ is .NET's query language over collections — similar to Python's list comprehensions + `filter/map` or JS's `.filter()/.map()`.

```csharp
// Filter
_store.Where(x => x.Unit == "kg")

// Find first match
_store.FirstOrDefault(x => x.Id == id)

// Transform
_store.Select(x => new { x.Id, x.Name })

// Sort
_store.OrderBy(x => x.Name)

// Python equivalents
[x for x in store if x.unit == "kg"]
next((x for x in store if x.id == id), None)
[{"id": x.id, "name": x.name} for x in store]
sorted(store, key=lambda x: x.name)
```

---

## Step 1 — Create a dedicated store class

Create `src/Nastart.Api/Features/Ingredients/IngredientStore.cs`:

```csharp
namespace Nastart.Api.Features.Ingredients;

/// <summary>
/// Thread-safe in-memory store.
/// This is temporary — replaced by EF Core + PostgreSQL in Lesson 04.
/// </summary>
public static class IngredientStore
{
    // static = shared across all requests (module-level state)
    // readonly = the reference is fixed, but list contents can change
    // List initializer = [] is the C# 12 shorthand for new List<Ingredient>()
    private static readonly List<Ingredient> _ingredients =
    [
        new() { Id = Guid.NewGuid(), Name = "Flour",  Unit = "kg",  CurrentPricePerUnit = 12_000m },
        new() { Id = Guid.NewGuid(), Name = "Sugar",  Unit = "kg",  CurrentPricePerUnit = 16_000m },
        new() { Id = Guid.NewGuid(), Name = "Butter", Unit = "kg",  CurrentPricePerUnit = 90_000m },
        new() { Id = Guid.NewGuid(), Name = "Eggs",   Unit = "pcs", CurrentPricePerUnit =  2_000m },
    ];
    // ↑ 12_000m — the 'm' suffix makes it a decimal literal. Underscores are allowed for readability.

    // Return a copy to prevent callers from mutating the internal list
    public static List<Ingredient> GetAll() => [.._ingredients];
    // ↑ [.._ingredients] = spread syntax, creates a new list with copied elements

    public static Ingredient? GetById(Guid id) =>
        _ingredients.FirstOrDefault(x => x.Id == id);
    // ↑ FirstOrDefault: returns the first match, or null if none found

    public static Ingredient Add(Ingredient ingredient)
    {
        ingredient.Id = Guid.NewGuid();
        ingredient.CreatedAt = DateTimeOffset.UtcNow;
        _ingredients.Add(ingredient);
        return ingredient;
    }

    public static bool Update(Guid id, Ingredient updated)
    {
        var existing = _ingredients.FirstOrDefault(x => x.Id == id);
        if (existing is null) return false;

        // Update mutable fields
        existing.Name = updated.Name;
        existing.Unit = updated.Unit;
        existing.CurrentPricePerUnit = updated.CurrentPricePerUnit;
        return true;
    }

    public static bool Delete(Guid id)
    {
        var existing = _ingredients.FirstOrDefault(x => x.Id == id);
        if (existing is null) return false;

        _ingredients.Remove(existing);
        return true;
    }
}
```

---

## Step 2 — Create request/response records

C# records are great for API request/response types. Unlike full classes, you declare all properties in one line.

Create `src/Nastart.Api/Features/Ingredients/IngredientRequests.cs`:

```csharp
namespace Nastart.Api.Features.Ingredients;

// Record for creating — excludes Id and CreatedAt (server sets those)
public record CreateIngredientRequest(
    string Name,          // positional properties — order matches the constructor
    string? Unit,
    decimal CurrentPricePerUnit
);

// Record for updating — all fields optional (null = don't change)
public record UpdateIngredientRequest(
    string? Name,
    string? Unit,
    decimal? CurrentPricePerUnit
);
// ↑ Compare Python: @dataclass with Optional fields, or TypeScript Partial<UpdateIngredientDto>
```

---

## Step 3 — Update the endpoints to use the store

Replace `IngredientsEndpoints.cs` with the fully working version:

```csharp
namespace Nastart.Api.Features.Ingredients;

public static class IngredientsEndpoints
{
    public static WebApplication MapIngredientsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/ingredients").WithTags("Ingredients");

        group.MapGet("/",          GetAll);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/",         Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);

        return app;
    }

    // ── GET /ingredients ───────────────────────────────────────────

    static IResult GetAll()
    {
        var ingredients = IngredientStore.GetAll();
        return TypedResults.Ok(ingredients);
    }

    // ── GET /ingredients/{id} ──────────────────────────────────────

    static IResult GetById(Guid id)
    {
        var ingredient = IngredientStore.GetById(id);

        // Pattern matching: is null / is not null
        return ingredient is not null
            ? TypedResults.Ok(ingredient)
            : TypedResults.NotFound();
    }

    // ── POST /ingredients ──────────────────────────────────────────

    static IResult Create(CreateIngredientRequest request)
    {
        // Map request DTO → entity
        // 'new Ingredient { ... }' = object initializer syntax
        var ingredient = new Ingredient
        {
            Name = request.Name,
            Unit = request.Unit,
            CurrentPricePerUnit = request.CurrentPricePerUnit,
        };

        var created = IngredientStore.Add(ingredient);
        return TypedResults.Created($"/ingredients/{created.Id}", created);
    }

    // ── PUT /ingredients/{id} ──────────────────────────────────────

    static IResult Update(Guid id, UpdateIngredientRequest request)
    {
        var existing = IngredientStore.GetById(id);
        if (existing is null) return TypedResults.NotFound();

        // Null-coalescing: ?? returns left side if not null, right side if null
        // Python equivalent: request.name or existing.name  (but type-safe)
        existing.Name = request.Name ?? existing.Name;
        existing.Unit = request.Unit ?? existing.Unit;
        existing.CurrentPricePerUnit = request.CurrentPricePerUnit ?? existing.CurrentPricePerUnit;

        IngredientStore.Update(id, existing);
        return TypedResults.Ok(existing);
    }

    // ── DELETE /ingredients/{id} ───────────────────────────────────

    static IResult Delete(Guid id)
    {
        var deleted = IngredientStore.Delete(id);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
```

---

## ✅ Run it and test every endpoint

```powershell
dotnet run
```

**Test GET all:**
```powershell
curl https://localhost:7xxx/ingredients
# Returns: JSON array of 4 seed ingredients
```

**Test POST (create):**
```powershell
curl -X POST https://localhost:7xxx/ingredients `
  -H "Content-Type: application/json" `
  -d '{"name":"Condensed Milk","unit":"can","currentPricePerUnit":12000}'
# Returns: 201 Created with the new ingredient (with Id assigned)
```

**Test GET by id** (copy an Id from the previous response):
```powershell
curl https://localhost:7xxx/ingredients/{paste-id-here}
# Returns: 200 OK with that ingredient  
```

**Test PUT (update):**
```powershell
curl -X PUT https://localhost:7xxx/ingredients/{paste-id-here} `
  -H "Content-Type: application/json" `
  -d '{"currentPricePerUnit": 13000}'
# Returns: 200 OK with updated price
```

**Test DELETE:**
```powershell
curl -X DELETE https://localhost:7xxx/ingredients/{paste-id-here}
# Returns: 204 No Content
```

---

## 🔑 Key Concepts

| Concept | What it is |
|---------|-----------|
| `static` field | Shared across all instances / requests — module-level state |
| `readonly` | The reference can't be reassigned (not a deep immutability guarantee) |
| `List<T>` | Typed list — like Python `list[T]` |
| LINQ `.Where()` `.FirstOrDefault()` `.Select()` | Query collections — like Python list comprehensions |
| `FirstOrDefault` | Returns first match or `null` (not an exception) |
| `record` | Immutable data type, value equality — ideal for DTOs |
| `??` (null-coalescing) | Return left if not null, otherwise right — like `x or y` in Python |
| `is null` / `is not null` | Pattern matching for null checks — preferred over `== null` |

---

> **Important**: The in-memory store loses data every time you restart the app.  
> That's fine — it's intentional scaffolding. Lesson 04 replaces it with PostgreSQL.

---

➡️ Next: [Lesson 04 — EF Core + PostgreSQL](04-ef-core-postgres.md)
