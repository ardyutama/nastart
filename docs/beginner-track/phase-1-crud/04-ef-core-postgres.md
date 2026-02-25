# Lesson 04: EF Core + PostgreSQL

## 🎯 What you'll build
Replace the in-memory `List<Ingredient>` with a real PostgreSQL database using Entity Framework Core. By the end, data survives app restarts.

---

## 🔧 The .NET Way

### EF Core (Entity Framework Core)

EF Core is .NET's ORM — it maps C# classes to database tables and lets you write C# instead of SQL.

| Concept | EF Core | Python SQLAlchemy | JS Prisma/Sequelize |
|---------|---------|-------------------|--------------------|
| ORM | EF Core | SQLAlchemy | Prisma / TypeORM |
| Connection + models | `DbContext` | `Session` | `PrismaClient` |
| Model definition | C# class with properties | Python class with `Column()` | Schema file / decorators |
| Migrations | `dotnet ef migrations add` | `alembic revision` | `prisma migrate` |
| Apply migrations | `dotnet ef database update` | `alembic upgrade head` | `prisma migrate deploy` |

### DbContext

`DbContext` is your database session. It holds `DbSet<T>` properties — one per table.

```csharp
public class NastartDbContext : DbContext
{
    // DbSet<Ingredient> = the "ingredients" table
    public DbSet<Ingredient> Ingredients { get; set; }
}
```

Compare:
```python
# SQLAlchemy
class NastartDb(Base):
    ...

# Then query: session.query(Ingredient).all()
```

### async/await

Database operations are always async in .NET, just like in Python asyncio or Node.js:

```csharp
// C#
var ingredients = await _db.Ingredients.ToListAsync();

// Python asyncio equivalent
ingredients = await db.execute(select(Ingredient))

// JS/Node equivalent
const ingredients = await Ingredient.findAll();
```

In .NET, async methods return `Task<T>` instead of `T`. Like Python's `Coroutine[T]`.

```csharp
// Sync:  IResult GetAll()
// Async: async Task<IResult> GetAll()
```

---

## Step 1 — Start PostgreSQL with Docker

Create `docker-compose.yml` at the repo root (if not already there):

```yaml
# docker-compose.yml
services:
  postgres:
    image: postgres:17-alpine          # lightweight Alpine-based image
    container_name: nastart_postgres
    environment:
      POSTGRES_DB: nastart             # database name
      POSTGRES_USER: nastart           # username
      POSTGRES_PASSWORD: nastart_dev   # password (dev only — never use in prod)
    ports:
      - "5432:5432"                    # expose port so the app can connect
    volumes:
      - nastart_postgres_data:/var/lib/postgresql/data   # persist data

volumes:
  nastart_postgres_data:
```

Start it:

```powershell
docker compose up -d       # -d = detached (background)
docker compose ps          # verify it's running → should show "healthy"
```

---

## Step 2 — Install EF Core packages

```powershell
cd src/Nastart.Api

# EF Core for PostgreSQL (uses Npgsql driver)
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL

# EF Core tools (for running migrations from CLI)
dotnet add package Microsoft.EntityFrameworkCore.Design
```

---

## Step 3 — Create the DbContext

Create `src/Nastart.Api/Shared/Data/NastartDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Shared.Data;

/// <summary>
/// The EF Core database context — the single entry point for all DB operations.
/// One DbContext represents one "unit of work" (typically one HTTP request).
/// </summary>
public class NastartDbContext(DbContextOptions<NastartDbContext> options)
    : DbContext(options)
    // ↑ Primary constructor — C# 12 syntax. Equivalent to:
    //   public NastartDbContext(DbContextOptions<NastartDbContext> options) : base(options) {}
{
    // One DbSet<T> per entity → maps to one table
    public DbSet<Ingredient> Ingredients { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure the Ingredient table
        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.HasKey(e => e.Id);               // primary key

            entity.Property(e => e.Name)
                .IsRequired()                       // NOT NULL
                .HasMaxLength(200);                 // VARCHAR(200)

            entity.Property(e => e.Unit)
                .HasMaxLength(50);

            entity.Property(e => e.CurrentPricePerUnit)
                .HasPrecision(18, 4);               // DECIMAL(18, 4) — good for money
        });
    }
}
```

---

## Step 4 — Register the DbContext in Program.cs

```csharp
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Shared.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Register DbContext with PostgreSQL connection string
// ↓ GetConnectionString("Default") reads from appsettings.json → ConnectionStrings → Default
builder.Services.AddDbContext<NastartDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
);
// Compare: SQLAlchemy's engine = create_engine("postgresql://...")
// Compare: Sequelize's new Sequelize("postgres://...")

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

## Step 5 — Add the connection string

Add to `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=nastart;Username=nastart;Password=nastart_dev"
  }
}
```

> **Note**: `appsettings.Development.json` should be listed in `.gitignore` if it contains real secrets. For this dev setup the password is throwaway, so it's okay to commit.

---

## Step 6 — Create and apply the first migration

```powershell
# (from the src/Nastart.Api folder)

# Install the dotnet-ef global tool if you haven't
dotnet tool install --global dotnet-ef

# Create the migration — generates C# code that describes the schema change
dotnet ef migrations add InitialCreate

# Apply it — creates the actual tables in PostgreSQL
dotnet ef database update
```

You should see output like:
```
Applying migration '20260224_InitialCreate'...
Done.
```

---

## Step 7 — Rewrite the endpoints to use the database

Replace `IngredientsEndpoints.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
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

    // ── GET /ingredients ───────────────────────────────────────────
    // NastartDbContext db  ← injected from the DI container automatically!
    // (called "minimal API parameter binding" — .NET resolves DI services from the container)

    static async Task<IResult> GetAll(NastartDbContext db)
    {
        // ToListAsync() = SELECT * FROM ingredients
        var ingredients = await db.Ingredients.ToListAsync();
        return TypedResults.Ok(ingredients);
    }

    // ── GET /ingredients/{id} ──────────────────────────────────────

    static async Task<IResult> GetById(Guid id, NastartDbContext db)
    {
        // FindAsync() = SELECT * FROM ingredients WHERE id = @id (uses primary key)
        var ingredient = await db.Ingredients.FindAsync(id);

        return ingredient is not null
            ? TypedResults.Ok(ingredient)
            : TypedResults.NotFound();
    }

    // ── POST /ingredients ──────────────────────────────────────────

    static async Task<IResult> Create(CreateIngredientRequest request, NastartDbContext db)
    {
        var ingredient = new Ingredient
        {
            Name = request.Name,
            Unit = request.Unit,
            CurrentPricePerUnit = request.CurrentPricePerUnit,
        };

        db.Ingredients.Add(ingredient);    // track the new entity
        await db.SaveChangesAsync();       // INSERT INTO ingredients ...

        return TypedResults.Created($"/ingredients/{ingredient.Id}", ingredient);
    }

    // ── PUT /ingredients/{id} ──────────────────────────────────────

    static async Task<IResult> Update(Guid id, UpdateIngredientRequest request, NastartDbContext db)
    {
        var ingredient = await db.Ingredients.FindAsync(id);
        if (ingredient is null) return TypedResults.NotFound();

        ingredient.Name = request.Name ?? ingredient.Name;
        ingredient.Unit = request.Unit ?? ingredient.Unit;
        ingredient.CurrentPricePerUnit = request.CurrentPricePerUnit ?? ingredient.CurrentPricePerUnit;

        await db.SaveChangesAsync();       // UPDATE ingredients SET ...

        return TypedResults.Ok(ingredient);
    }

    // ── DELETE /ingredients/{id} ───────────────────────────────────

    static async Task<IResult> Delete(Guid id, NastartDbContext db)
    {
        var ingredient = await db.Ingredients.FindAsync(id);
        if (ingredient is null) return TypedResults.NotFound();

        db.Ingredients.Remove(ingredient); // mark for deletion
        await db.SaveChangesAsync();       // DELETE FROM ingredients WHERE id = @id

        return TypedResults.NoContent();
    }
}
```

> **Key insight**: `NastartDbContext db` in the method signature is automatically injected by .NET's DI container — the same system that `builder.Services.AddDbContext<>()` registered it into. You don't call `new NastartDbContext()` yourself.

---

## ✅ Run it

```powershell
dotnet run
```

Create an ingredient, restart the app (`Ctrl+C`, then `dotnet run` again), then `GET /ingredients` — the data persists. ✅

---

## 🔑 Key Concepts

| Concept | What it is |
|---------|-----------|
| `DbContext` | Database session — one per HTTP request |
| `DbSet<T>` | Represents a database table |
| `SaveChangesAsync()` | Commits all pending INSERT/UPDATE/DELETE |
| `FindAsync(id)` | SELECT by primary key |
| `ToListAsync()` | SELECT all rows |
| `async Task<IResult>` | Async handler — returns a `Task` that resolves to `IResult` |
| `await` | Suspend execution until the async operation finishes |
| Migration | C# code that describes a schema change — the source of truth |
| `dotnet ef migrations add` | Generate migration from your DbContext changes |
| `dotnet ef database update` | Apply pending migrations to the database |
| DI parameter binding | Minimal APIs auto-inject registered services into handler parameters |

---

➡️ Next: [Lesson 05 — Validation + OpenAPI docs](05-validation-openapi.md)
