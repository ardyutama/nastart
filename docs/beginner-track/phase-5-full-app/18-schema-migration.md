# Lesson 18: Schema Migration — Adding User and Category

> **Phase**: 5 — Building the Full Application
> **Prerequisite**: Lesson 17. Your project has one `ingredients` table and runs.
> **What you build**: Add `users` and `categories` tables to your existing database without dropping anything.

---

## What You Will Learn

- How to add new tables to an existing database with EF Core migrations
- How to seed reference data (categories) at migration time
- `IEntityTypeConfiguration<T>` for the new entities
- Why you never drop and recreate in real projects — you always migrate forward

---

## The Problem With Starting Over

In Phase 1–4, you ran `dotnet ef database update` once against a fresh database. In real applications you can't drop and recreate — your production database has live data. Instead, you create **additive migrations** that only add what's new.

```
Before this lesson:          After this lesson:
─────────────────            ──────────────────────────
ingredients                  ingredients  (unchanged)
                             users        (new)
                             categories   (new)
```

---

## Step 1 — Create the User Entity

Create `src/Nastart.Api/Features/Auth/User.cs`:

```csharp
namespace Nastart.Api.Features.Auth;

public class User
{
    public Guid     Id               { get; set; }
    public required string Name      { get; set; }
    public required string Email     { get; set; }     // unique
    public required string PasswordHash { get; set; } // BCrypt hash, never plaintext
    public string?  BusinessName     { get; set; }
    public string?  BusinessType     { get; set; }     // bakery | cafe | catering | home
    public decimal  MinMarginPercent { get; set; } = 30m;
    public long?    TelegramId       { get; set; }
    public string?  TelegramUsername { get; set; }
    public bool     IsOnboarded      { get; set; }
    public DateTimeOffset CreatedAt  { get; set; } = DateTimeOffset.UtcNow;
}
```

---

## Step 2 — Create the Category Entity

Create `src/Nastart.Api/Features/Ingredients/Category.cs`:

```csharp
namespace Nastart.Api.Features.Ingredients;

public class Category
{
    public Guid     Id          { get; set; }
    public required string Name { get; set; }    // unique across all users (system-level)
    public string?  Description { get; set; }

    // Navigation — EF Core uses this to resolve Ingredient.CategoryId FK
    public ICollection<Ingredient> Ingredients { get; set; } = [];
}
```

---

## Step 3 — EF Core Configurations

Create `src/Nastart.Api/Shared/Data/Configurations/UserConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Auth;

namespace Nastart.Api.Shared.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(u => u.Name).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(300);
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.BusinessName).HasMaxLength(200);
        builder.Property(u => u.BusinessType).HasMaxLength(50);
        builder.Property(u => u.MinMarginPercent).HasColumnType("decimal(5,2)");

        // Email must be unique — two users can't share an email
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("idx_users_email");
    }
}
```

Create `src/Nastart.Api/Shared/Data/Configurations/CategoryConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Shared.Data.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Description).HasMaxLength(300);

        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasDatabaseName("idx_categories_name");

        // ── Seed data: 4 default categories ──────────────────────────
        // HasData seeds rows in the migration — they are inserted once and never repeated.
        // The IDs are hardcoded GUIDs so EF Core can track them across migrations.
        builder.HasData(
            new Category { Id = Guid.Parse("11111111-0000-0000-0000-000000000001"), Name = "Dry Goods",   Description = "Flour, sugar, rice, and dry staples" },
            new Category { Id = Guid.Parse("11111111-0000-0000-0000-000000000002"), Name = "Dairy",       Description = "Milk, butter, cheese, cream" },
            new Category { Id = Guid.Parse("11111111-0000-0000-0000-000000000003"), Name = "Spices",      Description = "Salt, pepper, cinnamon, vanilla" },
            new Category { Id = Guid.Parse("11111111-0000-0000-0000-000000000004"), Name = "Packaging",   Description = "Boxes, bags, labels" }
        );
    }
}
```

> **Why hardcoded GUIDs for seed data?**
>
> EF Core tracks seeded rows by their primary key. If you use `Guid.NewGuid()`, each time you run `dotnet ef migrations add` it generates a new GUID — EF Core thinks the old row was deleted and a new one was inserted. Hardcoded GUIDs = stable seed data across migrations.

---

## Step 4 — Register the New DbSets

Update `src/Nastart.Api/Shared/Data/NastartDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Auth;
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Shared.Data;

public class NastartDbContext(DbContextOptions<NastartDbContext> options) : DbContext(options)
{
    public DbSet<Ingredient> Ingredients { get; set; }
    public DbSet<User>       Users       { get; set; }    // ← new
    public DbSet<Category>   Categories  { get; set; }    // ← new

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // This scans the assembly for all IEntityTypeConfiguration<T> implementations
        // and applies them automatically — including UserConfiguration and CategoryConfiguration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NastartDbContext).Assembly);
    }
}
```

---

## Step 5 — Create and Apply the Migration

```bash
# Generate the migration — EF Core diffs the current model against the last migration
dotnet ef migrations add AddUsersAndCategories --project src/Nastart.Api

# Inspect what was generated — always read migrations before applying!
# Open: src/Nastart.Api/Migrations/<timestamp>_AddUsersAndCategories.cs
# You should see: CreateTable("users"), CreateTable("categories"), InsertData("categories", ...)

# Apply to the database
dotnet ef database update --project src/Nastart.Api
```

### What the generated migration looks like

```csharp
// The Up() method adds tables — it never touches existing data
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.CreateTable(
        name: "users",
        columns: table => new
        {
            id = table.Column<Guid>(nullable: false, defaultValueSql: "gen_random_uuid()"),
            name = table.Column<string>(maxLength: 200, nullable: false),
            email = table.Column<string>(maxLength: 300, nullable: false),
            // ...
        },
        constraints: table => table.PrimaryKey("PK_users", x => x.id));

    migrationBuilder.CreateTable(name: "categories", /* ... */);

    // Seed rows — inserted only once by this migration
    migrationBuilder.InsertData(
        table: "categories",
        columns: new[] { "id", "name", "description" },
        values: new object[,]
        {
            { new Guid("11111111-0000-0000-0000-000000000001"), "Dry Goods", "Flour, sugar, rice..." },
            // ...
        });
}

// The Down() method reverses the migration — used if you need to roll back
protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.DropTable(name: "users");
    migrationBuilder.DropTable(name: "categories");
}
```

---

## ✅ Verify It

```bash
# Connect to PostgreSQL and check the tables exist
docker exec -it nastart-postgres psql -U postgres -d nastart -c "\dt"
# Should show: ingredients, users, categories

# Check the seed data
docker exec -it nastart-postgres psql -U postgres -d nastart -c "SELECT * FROM categories;"
# Should show 4 rows
```

---

## Key Concepts

| Term | Definition |
|---|---|
| **Additive migration** | A migration that only adds — never drops existing tables or columns |
| **`HasData()`** | Seeds rows at migration time. Rows are tracked by PK across future migrations. |
| **Hardcoded seed GUIDs** | Required so EF Core can track seeded rows stably across migrations |
| **`ApplyConfigurationsFromAssembly()`** | Auto-discovers all `IEntityTypeConfiguration<T>` — no manual registration needed |
| **`Down()` method** | The rollback path — EF Core generates it automatically, reverses `Up()` |

---

## Next

Lesson 19 → **Registration** — now that the `users` table exists, build the `POST /auth/register` endpoint that creates the first user account.
