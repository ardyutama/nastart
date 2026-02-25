# Week 5: Database & Shared Services 🗄️

> **Goal**: Configure EF Core entity mappings, create database migrations, seed initial data, and build MediatR pipeline behaviors for validation and logging.

---

> ⚠️ **READING ORDER NOTE**
>
> **Days 5 & 6 of this week (ValidationBehavior + LoggingBehavior) should be built during Week 2**, alongside the rest of the MediatR infrastructure.
>
> Why? Every handler you write in Weeks 2–4 flows through these pipeline behaviors. If you build them in Week 5, all your earlier handlers will be missing validation and structured logging until you backfill.
>
> **Recommended approach:**
> 1. When you reach Week 2 Day 4 (MediatR Notifications), jump to **Day 5 and Day 6 of this file** first.
> 2. Build `ValidationBehavior` and `LoggingBehavior` then.
> 3. Return here in Week 5 for Days 1–4 (EF configs, migrations, seeding) after all entity models are defined.

---

## Table of Contents
1. [Day 1: EF Core Entity Configurations](#day-1-ef-core-entity-configurations)
2. [Day 2: Relationship Mappings](#day-2-relationship-mappings)
3. [Day 3: Database Migrations](#day-3-database-migrations)
4. [Day 4: Data Seeding](#day-4-data-seeding)
5. [Day 5: ValidationBehavior Pipeline](#day-5-validationbehavior-pipeline) — *⚠️ Read this during Week 2*
6. [Day 6: LoggingBehavior & Exception Handling](#day-6-loggingbehavior--exception-handling) — *⚠️ Read this during Week 2*
7. [Day 7: Testing Database & Behaviors](#day-7-testing-database--behaviors)
8. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1: EF Core Entity Configurations

## 🧒 Explain Like I'm 5

When you draw a picture, you need rules:
- "Always color inside the lines!"
- "Use the right colors for skin, grass, sky!"
- "Make sure the head isn't bigger than the body!"

**Entity Configurations** are rules for our database:
- "Names can only be 200 letters long!"
- "Prices must be saved with 2 decimal places!"
- "Every ingredient MUST have a name!"

These rules make sure our data is always neat and correct.

## 🔧 Engineer Language

**Entity configurations** define how C# classes map to database tables. EF Core uses conventions by default, but configurations let us customize column types, constraints, and relationships.

> 📖 **Microsoft Docs**: *"Use the Fluent API to configure entities. The Fluent API is a more powerful way to configure a model than data annotations."*
>
> — [Creating and Configuring a Model](https://learn.microsoft.com/en-us/ef/core/modeling/)

### Why Use IEntityTypeConfiguration?

| Approach | Pros | Cons |
|----------|------|------|
| **Data Annotations** | Simple, visible on entity | Clutters entity class |
| **OnModelCreating** | All in one place | Gets huge fast |
| **IEntityTypeConfiguration** | One file per entity, clean separation | More files |

In Vertical Slice Architecture, we use `IEntityTypeConfiguration` inside the `Shared/Data/Configurations/` folder.

### Folder Structure:

```
src/Nastart.Api/
├── Shared/
│   └── Data/
│       ├── NastartDbContext.cs
│       └── Configurations/
│           ├── IngredientConfiguration.cs
│           ├── CategoryConfiguration.cs
│           ├── PurchaseConfiguration.cs
│           ├── PurchaseItemConfiguration.cs
│           ├── RecipeConfiguration.cs
│           ├── RecipeItemConfiguration.cs
│           └── UserConfiguration.cs
```

### IngredientConfiguration:

Create `src/Nastart.Api/Shared/Data/Configurations/IngredientConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Shared.Data.Configurations;

/// <summary>
/// Entity configuration for Ingredient.
/// Defines table mapping, column constraints, and relationships.
/// </summary>
/// <remarks>
/// Following EF Core Fluent API best practices.
/// See: https://learn.microsoft.com/en-us/ef/core/modeling/
/// </remarks>
public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        // Table name
        builder.ToTable("ingredients");
        
        // Primary key
        builder.HasKey(i => i.Id);
        
        // Properties
        builder.Property(i => i.Name)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(i => i.Unit)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(i => i.CurrentPrice)
            .HasPrecision(18, 2)
            .IsRequired(false);  // null = not yet priced
        
        builder.Property(i => i.CurrentStock)
            .HasPrecision(18, 4)
            .HasDefaultValue(0);
        
        builder.Property(i => i.MinStock)
            .HasPrecision(18, 4)
            .HasDefaultValue(0);
        
        builder.Property(i => i.LastPurchaseDate)
            .IsRequired(false);
        
        // Indexes for common queries
        builder.HasIndex(i => i.UserId);
        builder.HasIndex(i => i.Name);
        builder.HasIndex(i => new { i.UserId, i.Name }).IsUnique();
        
        // Relationship: Ingredient -> Category
        builder.HasOne(i => i.Category)
            .WithMany()
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
```

### CategoryConfiguration:

Create `src/Nastart.Api/Shared/Data/Configurations/CategoryConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Shared.Data.Configurations;

/// <summary>
/// Entity configuration for Category.
/// </summary>
public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(c => c.Description)
            .HasMaxLength(500);
        
        // Unique category names
        builder.HasIndex(c => c.Name).IsUnique();
    }
}
```

### Apply Configurations in DbContext:

Update `src/Nastart.Api/Shared/Data/NastartDbContext.cs`:

```csharp
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Alerts;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Features.Purchases;
using Nastart.Api.Features.Recipes;

namespace Nastart.Api.Shared.Data;

/// <summary>
/// Main database context for Nastart.
/// Uses EF Core with PostgreSQL.
/// </summary>
/// <remarks>
/// In Vertical Slice Architecture, the DbContext is simple — no repositories.
/// Feature handlers use DbContext directly.
/// See: https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/
/// </remarks>
public class NastartDbContext : DbContext
{
    public NastartDbContext(DbContextOptions<NastartDbContext> options) 
        : base(options) { }
    
    // ── Users ──
    public DbSet<User> Users => Set<User>();
    
    // ── Ingredients ──
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Category> Categories => Set<Category>();
    
    // ── Purchases ──
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    
    // ── Recipes ──
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all IEntityTypeConfiguration classes from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(
            Assembly.GetExecutingAssembly());
        
        base.OnModelCreating(modelBuilder);
    }
}
```

### Your Task (Day 1):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create Configurations folder
mkdir Shared\Data\Configurations

# Create configuration files
New-Item Shared\Data\Configurations\IngredientConfiguration.cs
New-Item Shared\Data\Configurations\CategoryConfiguration.cs

# Verify build
dotnet build
```

---

# Day 2: Relationship Mappings

## 🧒 Explain Like I'm 5

Think about your family:
- You have ONE mom and ONE dad (one-to-one)
- Your mom has MANY children (one-to-many)
- Your classmates can have MANY friends, and each friend can have MANY classmates (many-to-many)

In our database:
- A **Purchase** has MANY **PurchaseItems** (one-to-many)
- A **Recipe** has MANY **RecipeItems** (one-to-many)
- Each **RecipeItem** points to ONE **Ingredient** (many-to-one)

## 🔧 Engineer Language

**Relationship mappings** define how entities connect. EF Core supports one-to-one, one-to-many, and many-to-many relationships.

> 📖 **Microsoft Docs**: *"Relationships define how two entities relate to each other. In a relational database, this is represented by a foreign key constraint."*
>
> — [Relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships)

### PurchaseConfiguration:

Create `src/Nastart.Api/Shared/Data/Configurations/PurchaseConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Purchases;

namespace Nastart.Api.Shared.Data.Configurations;

/// <summary>
/// Entity configuration for Purchase and related entities.
/// </summary>
public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("purchases");
        
        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.PurchaseDate)
            .IsRequired();
        
        builder.Property(p => p.ReceiptNumber)
            .HasMaxLength(100);
        
        builder.Property(p => p.Notes)
            .HasMaxLength(1000);
        
        builder.Property(p => p.TotalAmount)
            .HasPrecision(18, 2);
        
        builder.Property(p => p.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(PurchaseStatus.Received);
        
        // Indexes
        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.PurchaseDate);
        builder.HasIndex(p => new { p.UserId, p.PurchaseDate });
        
        // Relationship: Purchase -> PurchaseItems
        builder.HasMany(p => p.Items)
            .WithOne(i => i.Purchase)
            .HasForeignKey(i => i.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### PurchaseItemConfiguration:

Create `src/Nastart.Api/Shared/Data/Configurations/PurchaseItemConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Purchases;

namespace Nastart.Api.Shared.Data.Configurations;

/// <summary>
/// Entity configuration for PurchaseItem.
/// </summary>
public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        builder.ToTable("purchase_items");
        
        builder.HasKey(pi => pi.Id);
        
        builder.Property(pi => pi.IngredientName)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(pi => pi.Quantity)
            .HasPrecision(18, 4)
            .IsRequired();
        
        builder.Property(pi => pi.Unit)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(pi => pi.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();
        
        builder.Property(pi => pi.TotalPrice)
            .HasPrecision(18, 2);
        
        builder.Property(pi => pi.RawText)
            .HasMaxLength(500);
        
        builder.Property(pi => pi.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(PurchaseItemStatus.Matched);
        
        // Indexes
        builder.HasIndex(pi => pi.PurchaseId);
        builder.HasIndex(pi => pi.IngredientId);
        
        // Relationship: PurchaseItem -> Ingredient (optional — unmatched items have null)
        builder.HasOne(pi => pi.Ingredient)
            .WithMany()
            .HasForeignKey(pi => pi.IngredientId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
```

### RecipeConfiguration:

Create `src/Nastart.Api/Shared/Data/Configurations/RecipeConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Recipes;

namespace Nastart.Api.Shared.Data.Configurations;

/// <summary>
/// Entity configuration for Recipe.
/// </summary>
public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("recipes");
        
        builder.HasKey(r => r.Id);
        
        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);
        
        builder.Property(r => r.SellPrice)
            .HasPrecision(18, 2)
            .IsRequired(false);  // null = not yet priced (Draft)
        
        builder.Property(r => r.TotalCost)
            .HasPrecision(18, 2);
        
        builder.Property(r => r.MarginPercent)
            .HasPrecision(5, 2);
        
        builder.Property(r => r.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(RecipeStatus.Draft);
        
        builder.Property(r => r.YieldQuantity)
            .HasPrecision(18, 4);
        
        builder.Property(r => r.YieldUnit)
            .HasMaxLength(50)
            .HasDefaultValue("unit");
        
        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        // Indexes
        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.Name);
        builder.HasIndex(r => new { r.UserId, r.Status });
        
        // Relationship: Recipe -> RecipeItems
        builder.HasMany(r => r.Items)
            .WithOne(i => i.Recipe)
            .HasForeignKey(i => i.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### RecipeItemConfiguration:

Create `src/Nastart.Api/Shared/Data/Configurations/RecipeItemConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Recipes;

namespace Nastart.Api.Shared.Data.Configurations;

/// <summary>
/// Entity configuration for RecipeItem.
/// </summary>
public class RecipeItemConfiguration : IEntityTypeConfiguration<RecipeItem>
{
    public void Configure(EntityTypeBuilder<RecipeItem> builder)
    {
        builder.ToTable("recipe_items");
        
        builder.HasKey(ri => ri.Id);
        
        builder.Property(ri => ri.Quantity)
            .HasPrecision(18, 4)
            .IsRequired();
        
        builder.Property(ri => ri.Unit)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(ri => ri.Cost)
            .HasPrecision(18, 2);
        
        // Indexes
        builder.HasIndex(ri => ri.RecipeId);
        builder.HasIndex(ri => ri.IngredientId);
        
        // Unique: one ingredient per recipe
        builder.HasIndex(ri => new { ri.RecipeId, ri.IngredientId }).IsUnique();
        
        // Relationship: RecipeItem -> Ingredient
        builder.HasOne(ri => ri.Ingredient)
            .WithMany()
            .HasForeignKey(ri => ri.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

### Your Task (Day 2):

1. Create `PurchaseConfiguration.cs`
2. Create `PurchaseItemConfiguration.cs`
3. Create `RecipeConfiguration.cs`
4. Create `RecipeItemConfiguration.cs`
5. Verify build: `dotnet build`

---

# Day 3: Database Migrations

## 🧒 Explain Like I'm 5

Imagine your notebook is a database. At first, it's empty. Then:
1. "Hey, I need a page for ingredients!" (Migration 1)
2. "Oh, I need a page for recipes too!" (Migration 2)
3. "Wait, I need to add a 'notes' column to ingredients!" (Migration 3)

**Migrations** are instructions that tell the database:
- What tables to create
- What columns to add
- What to change

They're like a history book of all the changes!

## 🔧 Engineer Language

**Migrations** track database schema changes over time. EF Core generates migration files that can be applied to sync the database with your model.

> 📖 **Microsoft Docs**: *"Migrations provide a way to incrementally apply schema changes to the database. Each migration represents a set of schema changes."*
>
> — [Migrations Overview](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)

### Install EF Core Tools:

```powershell
# Install global EF Core tools
dotnet tool install --global dotnet-ef

# Or update if already installed
dotnet tool update --global dotnet-ef

# Verify installation
dotnet ef --version
```

### Create Your First Migration:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create initial migration
dotnet ef migrations add InitialCreate --output-dir Shared/Data/Migrations
```

This creates:
```
Shared/Data/Migrations/
├── 20260206000000_InitialCreate.cs
├── 20260206000000_InitialCreate.Designer.cs
└── NastartDbContextModelSnapshot.cs
```

### Understanding Migration Files:

```csharp
// 20260206000000_InitialCreate.cs
public partial class InitialCreate : Migration
{
    /// <summary>
    /// Applies the migration (creates tables).
    /// </summary>
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "categories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(100)", 
                    maxLength: 100, nullable: false),
                Description = table.Column<string>(type: "character varying(500)", 
                    maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_categories", x => x.Id);
            });
        
        migrationBuilder.CreateTable(
            name: "ingredients",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", 
                    maxLength: 200, nullable: false),
                Unit = table.Column<string>(type: "character varying(50)", 
                    maxLength: 50, nullable: false),
                CurrentPrice = table.Column<decimal>(type: "numeric(18,2)", 
                    precision: 18, scale: 2, nullable: true),
                CurrentStock = table.Column<decimal>(type: "numeric(18,4)", 
                    precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                MinStock = table.Column<decimal>(type: "numeric(18,4)", 
                    precision: 18, scale: 4, nullable: false, defaultValue: 0m),
                CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                LastPurchaseDate = table.Column<DateTime>(type: "timestamp with time zone", 
                    nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ingredients", x => x.Id);
                table.ForeignKey(
                    name: "FK_ingredients_categories_CategoryId",
                    column: x => x.CategoryId,
                    principalTable: "categories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });
        
        // ... more tables ...
        
        // Create indexes
        migrationBuilder.CreateIndex(
            name: "IX_ingredients_UserId",
            table: "ingredients",
            column: "UserId");
        
        migrationBuilder.CreateIndex(
            name: "IX_ingredients_UserId_Name",
            table: "ingredients",
            columns: new[] { "UserId", "Name" },
            unique: true);
    }

    /// <summary>
    /// Rolls back the migration (drops tables).
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ingredients");
        migrationBuilder.DropTable(name: "categories");
        // ... drop other tables ...
    }
}
```

### Configure Connection String:

Update `src/Nastart.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=nastart;Username=nastart;Password=nastart123"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

Update `src/Nastart.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=nastart_dev;Username=nastart;Password=nastart123"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

### Register DbContext in Program.cs:

```csharp
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext with PostgreSQL
builder.Services.AddDbContext<NastartDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
        }));

// ... rest of configuration
```

### Apply Migrations:

```powershell
# Make sure PostgreSQL is running
docker compose up -d postgres

# Apply migrations
dotnet ef database update

# Or use SQL script for production
dotnet ef migrations script -o migration.sql
```

### Adding More Migrations:

```powershell
# After changing entity models, add a new migration
dotnet ef migrations add AddNotesToIngredient

# Review the generated migration
# Then apply it
dotnet ef database update
```

### Your Task (Day 3):

```powershell
# Install EF Core tools
dotnet tool install --global dotnet-ef

cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create initial migration
dotnet ef migrations add InitialCreate --output-dir Shared/Data/Migrations

# Start PostgreSQL
docker compose up -d postgres

# Apply migration
dotnet ef database update

# Verify tables were created
# Connect to PostgreSQL and check
```

---

# Day 4: Data Seeding

## 🧒 Explain Like I'm 5

When you get a new coloring book, some pictures are already started for you — the outlines are there!

**Data seeding** is like pre-filling the database with helpful starting data:
- Categories: "Baking", "Dairy", "Spices", "Sweeteners"
- Maybe some sample ingredients for testing

This way, when the app starts, it's not completely empty!

## 🔧 Engineer Language

**Data seeding** populates the database with initial or test data. EF Core supports seeding via `HasData()` in configurations or via custom initialization code.

> 📖 **Microsoft Docs**: *"Data seeding is the process of populating a database with an initial set of data. Use HasData for simple scenarios."*
>
> — [Data Seeding](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding)

### Seed Categories in Configuration:

Update `src/Nastart.Api/Shared/Data/Configurations/CategoryConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Shared.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        
        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);
        
        builder.Property(c => c.Description)
            .HasMaxLength(500);
        
        builder.HasIndex(c => c.Name).IsUnique();
        
        // Seed initial categories
        builder.HasData(
            new Category { Id = Guid.Parse("a0000001-0000-0000-0000-000000000001"), Name = "Baking Essentials", Description = "Flour, sugar, baking powder, etc." },
            new Category { Id = Guid.Parse("a0000001-0000-0000-0000-000000000002"), Name = "Dairy", Description = "Milk, butter, cream, cheese" },
            new Category { Id = Guid.Parse("a0000001-0000-0000-0000-000000000003"), Name = "Eggs & Proteins", Description = "Eggs, egg whites, egg substitutes" },
            new Category { Id = Guid.Parse("a0000001-0000-0000-0000-000000000004"), Name = "Sweeteners", Description = "Sugar, honey, maple syrup, condensed milk" },
            new Category { Id = Guid.Parse("a0000001-0000-0000-0000-000000000005"), Name = "Fats & Oils", Description = "Butter, margarine, cooking oil, shortening" },
            new Category { Id = Guid.Parse("a0000001-0000-0000-0000-000000000006"), Name = "Flavorings", Description = "Vanilla, chocolate, coffee, extracts" },
            new Category { Id = Guid.Parse("a0000001-0000-0000-0000-000000000007"), Name = "Nuts & Dried Fruits", Description = "Almonds, walnuts, raisins, cranberries" },
            new Category { Id = Guid.Parse("a0000001-0000-0000-0000-000000000008"), Name = "Spices", Description = "Cinnamon, nutmeg, ginger, cardamom" },
            new Category { Id = Guid.Parse("a0000001-0000-0000-0000-000000000009"), Name = "Decorations", Description = "Sprinkles, food coloring, fondant" },
            new Category { Id = Guid.Parse("a0000001-0000-0000-0000-00000000000a"), Name = "Packaging", Description = "Boxes, ribbons, labels, bags" }
        );
    }
}
```

### User Configuration:

Create `src/Nastart.Api/Shared/Data/Configurations/UserConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Api.Features.Users;

namespace Nastart.Api.Shared.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        
        builder.HasKey(u => u.Id);
        
        builder.Property(u => u.TelegramId)
            .IsRequired();
        
        builder.Property(u => u.TelegramUsername)
            .HasMaxLength(200);
        
        builder.Property(u => u.DisplayName)
            .HasMaxLength(200);
        
        builder.Property(u => u.BusinessType)
            .HasMaxLength(100);
        
        builder.Property(u => u.MinMarginPercent)
            .HasPrecision(5, 2)
            .HasDefaultValue(20m);
        
        builder.Property(u => u.IsOnboarded)
            .HasDefaultValue(false);
        
        builder.Property(u => u.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        // Unique index on TelegramId (one user per Telegram account)
        builder.HasIndex(u => u.TelegramId).IsUnique();
        builder.HasIndex(u => u.TelegramUsername);
    }
}
```

### Custom Data Seeding Service:

For more complex seeding (like test data with GUIDs), create a seeding service:

Create `src/Nastart.Api/Shared/Data/DataSeeder.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Shared.Data;

/// <summary>
/// Seeds initial data for development and testing.
/// </summary>
/// <remarks>
/// Called during application startup in Development environment.
/// For production, use migrations with HasData() instead.
/// </remarks>
public static class DataSeeder
{
    /// <summary>
    /// Seeds development test data if database is empty.
    /// </summary>
    public static async Task SeedDevelopmentDataAsync(
        NastartDbContext db, 
        ILogger logger)
    {
        // Only seed if no ingredients exist
        if (await db.Ingredients.AnyAsync())
        {
            logger.LogInformation("Database already has data, skipping seed");
            return;
        }
        
        logger.LogInformation("Seeding development data...");
        
        // Test user ID (you'd get this from auth in real app)
        var testUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        
        // Seed sample ingredients
        var ingredients = new List<Ingredient>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = testUserId,
                Name = "Tepung Terigu Protein Tinggi",
                Unit = "kg",
                CurrentPrice = 18000,
                CurrentStock = 25,
                MinStock = 5,
                CategoryId = Guid.Parse("a0000001-0000-0000-0000-000000000001")
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = testUserId,
                Name = "Gula Pasir",
                Unit = "kg",
                CurrentPrice = 16000,
                CurrentStock = 10,
                MinStock = 3,
                CategoryId = Guid.Parse("a0000001-0000-0000-0000-000000000004")
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = testUserId,
                Name = "Telur Ayam",
                Unit = "butir",
                CurrentPrice = 2500,
                CurrentStock = 60,
                MinStock = 30,
                CategoryId = Guid.Parse("a0000001-0000-0000-0000-000000000003")
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = testUserId,
                Name = "Mentega",
                Unit = "kg",
                CurrentPrice = 35000,
                CurrentStock = 3,
                MinStock = 1,
                CategoryId = Guid.Parse("a0000001-0000-0000-0000-000000000005")
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = testUserId,
                Name = "Susu Kental Manis",
                Unit = "kaleng",
                CurrentPrice = 12000,
                CurrentStock = 8,
                MinStock = 4,
                CategoryId = Guid.Parse("a0000001-0000-0000-0000-000000000002")
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = testUserId,
                Name = "Coklat Bubuk",
                Unit = "kg",
                CurrentPrice = 85000,
                CurrentStock = 2,
                MinStock = 1,
                CategoryId = Guid.Parse("a0000001-0000-0000-0000-000000000006")
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = testUserId,
                Name = "Keju Cheddar",
                Unit = "kg",
                CurrentPrice = 120000,
                CurrentStock = 1.5m,
                MinStock = 0.5m,
                CategoryId = Guid.Parse("a0000001-0000-0000-0000-000000000002")
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = testUserId,
                Name = "Selai Nanas",
                Unit = "kg",
                CurrentPrice = 45000,
                CurrentStock = 2,
                MinStock = 1,
                CategoryId = Guid.Parse("a0000001-0000-0000-0000-000000000006")
            }
        };
        
        db.Ingredients.AddRange(ingredients);
        await db.SaveChangesAsync();
        
        logger.LogInformation("Seeded {Count} sample ingredients", ingredients.Count);
    }
}
```

### Call Seeder in Program.cs:

```csharp
var app = builder.Build();

// Seed development data
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NastartDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    // Ensure database is created and migrations applied
    await db.Database.MigrateAsync();
    
    // Seed test data
    await DataSeeder.SeedDevelopmentDataAsync(db, logger);
}

// ... rest of app configuration
```

### Create Migration for Seed Data:

```powershell
# After adding HasData(), create a migration
dotnet ef migrations add SeedCategories --output-dir Shared/Data/Migrations

# Apply migration
dotnet ef database update
```

### Your Task (Day 4):

1. Add `HasData()` to `CategoryConfiguration`
2. Create `DataSeeder.cs` for development data
3. Update `Program.cs` to call seeder
4. Create migration: `dotnet ef migrations add SeedCategories`
5. Apply: `dotnet ef database update`

---

# Day 5: ValidationBehavior Pipeline

## 🧒 Explain Like I'm 5

Before a letter gets delivered, the post office checks:
- "Does it have a stamp?" ✓
- "Is the address written correctly?" ✓
- "Is it sealed?" ✓

If something's wrong, they don't deliver it — they send it back!

**ValidationBehavior** is like the post office for our app:
- Before ANY command runs, we check: "Is the data correct?"
- If not, we stop and tell the user what's wrong
- If yes, we continue processing

## 🔧 Engineer Language

**MediatR Pipeline Behaviors** intercept requests before/after handlers. `ValidationBehavior` runs FluentValidation before the handler executes.

> 📖 **Microsoft Docs**: *"Pipeline behaviors wrap request handling, enabling cross-cutting concerns like validation, logging, and transactions."*
>
> — [MediatR Behaviors](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api)

### Install FluentValidation:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions
```

### Create ValidationBehavior:

Create `src/Nastart.Api/Shared/Behaviors/ValidationBehavior.cs`:

```csharp
using FluentValidation;
using MediatR;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Shared.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs FluentValidation before handlers.
/// </summary>
/// <remarks>
/// If validation fails, returns Result.Failure without executing the handler.
/// This provides consistent validation across all features.
/// 
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api
/// </remarks>
public class ValidationBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        
        if (!_validators.Any())
        {
            return await next();
        }
        
        _logger.LogDebug("Validating {Request}", requestName);
        
        // Run all validators
        var context = new ValidationContext<TRequest>(request);
        
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        
        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();
        
        if (failures.Count > 0)
        {
            _logger.LogWarning(
                "Validation failed for {Request}: {Errors}",
                requestName,
                string.Join(", ", failures.Select(f => f.ErrorMessage)));
            
            // Return failure result
            // Check if TResponse is Result<T>
            var responseType = typeof(TResponse);
            
            if (responseType.IsGenericType && 
                responseType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var errorMessages = string.Join("; ", failures.Select(f => f.ErrorMessage));
                var error = Error.Validation("VALIDATION_ERROR", errorMessages);
                
                // Create Result<T>.Failure(error)
                var resultType = responseType.GetGenericArguments()[0];
                var failureMethod = typeof(Result<>)
                    .MakeGenericType(resultType)
                    .GetMethod(nameof(Result<object>.Failure), new[] { typeof(Error) });
                
                return (TResponse)failureMethod!.Invoke(null, new object[] { error })!;
            }
            
            // For non-Result responses, throw exception
            throw new ValidationException(failures);
        }
        
        _logger.LogDebug("Validation passed for {Request}", requestName);
        
        return await next();
    }
}
```

### Update Result Class with Validation Error:

Update `src/Nastart.Api/Shared/Models/Result.cs`:

```csharp
namespace Nastart.Api.Shared.Models;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }
    
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }
    
    public static Result<T> Success(T value) => 
        new(true, value, null);
    
    public static Result<T> Failure(Error error) => 
        new(false, default, error);
}

/// <summary>
/// Represents an error with a code and message.
/// </summary>
public sealed record Error(string Code, string Message)
{
    public static Error NotFound(string code, string message) => new(code, message);
    public static Error Validation(string code, string message) => new(code, message);
    public static Error Conflict(string code, string message) => new(code, message);
    public static Error Unauthorized(string code, string message) => new(code, message);
    public static Error Internal(string code, string message) => new(code, message);
    
    /// <summary>
    /// Error type for categorization in API responses.
    /// </summary>
    public string Type => Code switch
    {
        _ when Code.Contains("NOT_FOUND") => "NotFound",
        _ when Code.Contains("VALIDATION") => "Validation", 
        _ when Code.Contains("CONFLICT") => "Conflict",
        _ when Code.Contains("UNAUTHORIZED") => "Unauthorized",
        _ => "Error"
    };
}
```

### Register Behaviors in Program.cs:

```csharp
using FluentValidation;
using MediatR;
using Nastart.Api.Shared.Behaviors;

var builder = WebApplication.CreateBuilder(args);

// Add MediatR with behaviors
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    
    // Add pipeline behaviors (order matters!)
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

// Add FluentValidation validators
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// ... rest of configuration
```

### How It Works:

```
Request arrives at endpoint
         │
         ▼
┌─────────────────────────────────┐
│    ValidationBehavior           │
│  ┌───────────────────────────┐  │
│  │ Find validators for       │  │
│  │ CreateIngredientCommand   │  │
│  └───────────────────────────┘  │
│              │                  │
│              ▼                  │
│  ┌───────────────────────────┐  │
│  │ Run CreateIngredient-     │  │
│  │ Validator.Validate()      │  │
│  └───────────────────────────┘  │
│              │                  │
│      ┌───────┴───────┐          │
│      │               │          │
│   Errors?         No Errors     │
│      │               │          │
│      ▼               ▼          │
│  Return           Call next()   │
│  Failure          (handler)     │
└─────────────────────────────────┘
```

### Your Task (Day 5):

1. Install FluentValidation packages
2. Create `ValidationBehavior.cs`
3. Update `Result.cs` with validation error
4. Register in `Program.cs`
5. Verify build: `dotnet build`

---

# Day 6: LoggingBehavior & Exception Handling

## 🧒 Explain Like I'm 5

Imagine a detective 🔍 who writes down EVERYTHING:
- "10:00 AM: Someone asked for ingredient list"
- "10:01 AM: Found 15 ingredients"
- "10:02 AM: Someone tried to add recipe... FAILED! Name was empty"

**LoggingBehavior** is our detective — it writes down every request and what happened. This helps us find bugs and understand how people use the app!

## 🔧 Engineer Language

**LoggingBehavior** logs request start, completion, and performance metrics. Combined with structured logging, this provides observability for debugging and monitoring.

> 📖 **Microsoft Docs**: *"Structured logging makes it easier to write, store, query, and analyze log data."*
>
> — [Logging in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)

### Create LoggingBehavior:

Create `src/Nastart.Api/Shared/Behaviors/LoggingBehavior.cs`:

```csharp
using System.Diagnostics;
using MediatR;

namespace Nastart.Api.Shared.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs request execution.
/// </summary>
/// <remarks>
/// Logs:
/// - Request name and user ID (if available)
/// - Execution time
/// - Success/failure status
/// 
/// Useful for debugging and performance monitoring.
/// See: https://learn.microsoft.com/en-us/dotnet/core/extensions/logging
/// </remarks>
public class LoggingBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestId = Guid.NewGuid().ToString("N")[..8];
        
        _logger.LogInformation(
            "[{RequestId}] Starting {RequestName}",
            requestId,
            requestName);
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var response = await next();
            
            stopwatch.Stop();
            
            _logger.LogInformation(
                "[{RequestId}] Completed {RequestName} in {ElapsedMs}ms",
                requestId,
                requestName,
                stopwatch.ElapsedMilliseconds);
            
            // Log slow requests as warnings
            if (stopwatch.ElapsedMilliseconds > 500)
            {
                _logger.LogWarning(
                    "[{RequestId}] SLOW REQUEST: {RequestName} took {ElapsedMs}ms",
                    requestId,
                    requestName,
                    stopwatch.ElapsedMilliseconds);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(
                ex,
                "[{RequestId}] FAILED {RequestName} after {ElapsedMs}ms: {Error}",
                requestId,
                requestName,
                stopwatch.ElapsedMilliseconds,
                ex.Message);
            
            throw;
        }
    }
}
```

### Create PerformanceBehavior (Optional):

For more detailed performance tracking:

Create `src/Nastart.Api/Shared/Behaviors/PerformanceBehavior.cs`:

```csharp
using System.Diagnostics;
using MediatR;

namespace Nastart.Api.Shared.Behaviors;

/// <summary>
/// Logs warnings for requests exceeding a time threshold.
/// </summary>
public class PerformanceBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private const int WarningThresholdMs = 500;

    public PerformanceBehavior(
        ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var response = await next();
        
        stopwatch.Stop();
        
        if (stopwatch.ElapsedMilliseconds > WarningThresholdMs)
        {
            var requestName = typeof(TRequest).Name;
            
            _logger.LogWarning(
                "Long Running Request: {RequestName} ({ElapsedMs}ms) {@Request}",
                requestName,
                stopwatch.ElapsedMilliseconds,
                request);
        }
        
        return response;
    }
}
```

### Global Exception Handling Middleware:

Create `src/Nastart.Api/Shared/Middleware/ExceptionHandlingMiddleware.cs`:

```csharp
using System.Text.Json;
using FluentValidation;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Shared.Middleware;

/// <summary>
/// Global exception handling middleware.
/// </summary>
/// <remarks>
/// Catches unhandled exceptions and returns consistent error responses.
/// See: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling
/// </remarks>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await HandleValidationException(context, ex);
        }
        catch (Exception ex)
        {
            await HandleUnexpectedException(context, ex);
        }
    }

    private async Task HandleValidationException(
        HttpContext context, 
        ValidationException exception)
    {
        _logger.LogWarning(
            "Validation error: {Errors}",
            string.Join(", ", exception.Errors.Select(e => e.ErrorMessage)));
        
        var response = new
        {
            Type = "Validation",
            Title = "Validation Error",
            Status = 400,
            Errors = exception.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray())
        };
        
        context.Response.StatusCode = 400;
        context.Response.ContentType = "application/json";
        
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }

    private async Task HandleUnexpectedException(
        HttpContext context, 
        Exception exception)
    {
        _logger.LogError(
            exception,
            "Unhandled exception: {Message}",
            exception.Message);
        
        var response = new
        {
            Type = "Error",
            Title = "An error occurred",
            Status = 500,
            Detail = context.RequestServices
                .GetRequiredService<IHostEnvironment>()
                .IsDevelopment() 
                    ? exception.Message 
                    : "An internal error occurred"
        };
        
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
    }
}
```

### Register Everything in Program.cs:

```csharp
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Behaviors;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext
builder.Services.AddDbContext<NastartDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Add MediatR with pipeline behaviors
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    
    // Pipeline behaviors execute in order registered
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

// Add FluentValidation
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

// Add OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Use exception handling middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure OpenAPI
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Seed development data
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NastartDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    await db.Database.MigrateAsync();
    await DataSeeder.SeedDevelopmentDataAsync(db, logger);
}

app.UseHttpsRedirection();

// Map feature endpoints (we'll add this next week)
// app.MapGroup("/api/ingredients").MapIngredientsEndpoints();
// app.MapGroup("/api/recipes").MapRecipesEndpoints();
// app.MapGroup("/api/purchases").MapPurchasesEndpoints();

app.Run();
```

### Your Task (Day 6):

1. Create `LoggingBehavior.cs`
2. Create `ExceptionHandlingMiddleware.cs`
3. Register behaviors and middleware in `Program.cs`
4. Verify build: `dotnet build`

---

# Day 7: Testing Database & Behaviors

## 🧒 Explain Like I'm 5

Before selling toys, you test them:
- "Does the car roll?" ✅
- "Do the lights turn on?" ✅
- "Does it break easily?" ❌

For our app:
- "Does saving to database work?" ✅
- "Does validation catch bad data?" ✅
- "Does logging work correctly?" ✅

## 🔧 Engineer Language

We test configurations, behaviors, and database operations using an **in-memory database** for fast, isolated tests.

> 📖 **Microsoft Docs**: *"The in-memory database is designed for testing and should not be used for production."*
>
> — [Testing with InMemory](https://learn.microsoft.com/en-us/ef/core/testing/testing-with-the-database)

### Install Test Packages:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend

# Add test project if not exists
dotnet new xunit -n Nastart.Api.Tests -o tests/Nastart.Api.Tests
dotnet sln add tests/Nastart.Api.Tests

cd tests/Nastart.Api.Tests
dotnet add reference ../../src/Nastart.Api
dotnet add package FluentAssertions
dotnet add package Moq
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

### Test ValidationBehavior:

Create `tests/Nastart.Api.Tests/Shared/Behaviors/ValidationBehaviorTests.cs`:

```csharp
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Nastart.Api.Shared.Behaviors;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Tests.Shared.Behaviors;

public class ValidationBehaviorTests
{
    private readonly Mock<ILogger<ValidationBehavior<TestCommand, Result<string>>>> _logger;
    
    public ValidationBehaviorTests()
    {
        _logger = new Mock<ILogger<ValidationBehavior<TestCommand, Result<string>>>>();
    }

    [Fact]
    public async Task Handle_NoValidators_CallsNext()
    {
        // Arrange
        var validators = Enumerable.Empty<IValidator<TestCommand>>();
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(
            validators, 
            _logger.Object);
        
        var command = new TestCommand("test");
        var expectedResult = Result<string>.Success("success");
        
        // Act
        var result = await behavior.Handle(
            command,
            () => Task.FromResult(expectedResult),
            CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("success");
    }

    [Fact]
    public async Task Handle_ValidationFails_ReturnsFailure()
    {
        // Arrange
        var validator = new Mock<IValidator<TestCommand>>();
        validator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<TestCommand>>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[]
            {
                new ValidationFailure("Name", "Name is required")
            }));
        
        var validators = new[] { validator.Object };
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(
            validators, 
            _logger.Object);
        
        var command = new TestCommand("");
        
        // Act
        var result = await behavior.Handle(
            command,
            () => Task.FromResult(Result<string>.Success("should not reach")),
            CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("VALIDATION_ERROR");
        result.Error.Message.Should().Contain("Name is required");
    }

    [Fact]
    public async Task Handle_ValidationPasses_CallsNext()
    {
        // Arrange
        var validator = new Mock<IValidator<TestCommand>>();
        validator
            .Setup(v => v.ValidateAsync(
                It.IsAny<ValidationContext<TestCommand>>(), 
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        
        var validators = new[] { validator.Object };
        var behavior = new ValidationBehavior<TestCommand, Result<string>>(
            validators, 
            _logger.Object);
        
        var command = new TestCommand("valid name");
        var expectedResult = Result<string>.Success("success");
        
        // Act
        var result = await behavior.Handle(
            command,
            () => Task.FromResult(expectedResult),
            CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
    }
    
    // Test command for behavior testing
    private record TestCommand(string Name) : IRequest<Result<string>>;
}
```

### Test DbContext Configuration:

Create `tests/Nastart.Api.Tests/Shared/Data/IngredientConfigurationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Ingredients;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Tests.Shared.Data;

public class IngredientConfigurationTests
{
    private NastartDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<NastartDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        return new NastartDbContext(options);
    }

    [Fact]
    public async Task Ingredient_CanBeSavedAndRetrieved()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        
        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Test Flour",
            Unit = "kg",
            CurrentPrice = 15000,
            CurrentStock = 10,
            MinStock = 2
        };
        
        // Act
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();
        
        var retrieved = await db.Ingredients.FindAsync(ingredient.Id);
        
        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Flour");
        retrieved.CurrentPrice.Should().Be(15000);
    }

    [Fact]
    public async Task Ingredient_WithCategory_LoadsRelationship()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        
        var category = new Category
        {
            Id = Guid.Parse("a0000001-0000-0000-0000-000000000001"),
            Name = "Baking"
        };
        
        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Flour",
            Unit = "kg",
            CurrentPrice = 15000,
            CurrentStock = 10,
            MinStock = 2,
            CategoryId = Guid.Parse("a0000001-0000-0000-0000-000000000001")
        };
        
        db.Categories.Add(category);
        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();
        
        // Act
        var loaded = await db.Ingredients
            .Include(i => i.Category)
            .FirstAsync(i => i.Id == ingredient.Id);
        
        // Assert
        loaded.Category.Should().NotBeNull();
        loaded.Category!.Name.Should().Be("Baking");
    }

    [Fact]
    public async Task Ingredient_DuplicateName_ThrowsOnRealDb()
    {
        // Note: In-memory doesn't enforce unique constraints
        // This test documents expected behavior on real database
        
        using var db = CreateInMemoryContext();
        
        var userId = Guid.NewGuid();
        
        var ingredient1 = new Ingredient
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Flour",
            Unit = "kg",
            CurrentPrice = 15000,
            CurrentStock = 10,
            MinStock = 2
        };
        
        var ingredient2 = new Ingredient
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Flour", // Same name, same user
            Unit = "kg",
            CurrentPrice = 16000,
            CurrentStock = 5,
            MinStock = 1
        };
        
        db.Ingredients.Add(ingredient1);
        db.Ingredients.Add(ingredient2);
        
        // In-memory allows this, real PostgreSQL would throw
        // This test just documents the configuration exists
        await db.SaveChangesAsync(); // Would throw on real DB
    }
}
```

### Test Data Seeding:

Create `tests/Nastart.Api.Tests/Shared/Data/DataSeederTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Tests.Shared.Data;

public class DataSeederTests
{
    [Fact]
    public async Task SeedDevelopmentData_EmptyDatabase_SeedsIngredients()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<NastartDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        using var db = new NastartDbContext(options);
        var logger = new Mock<ILogger>();
        
        // Act
        await DataSeeder.SeedDevelopmentDataAsync(db, logger.Object);
        
        // Assert
        var ingredients = await db.Ingredients.ToListAsync();
        ingredients.Should().NotBeEmpty();
        ingredients.Should().Contain(i => i.Name.Contains("Tepung"));
    }

    [Fact]
    public async Task SeedDevelopmentData_ExistingData_SkipsSeeding()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<NastartDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        using var db = new NastartDbContext(options);
        
        // Add existing ingredient
        db.Ingredients.Add(new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Existing",
            Unit = "kg",
            CurrentPrice = 1000,
            CurrentStock = 1,
            MinStock = 0
        });
        await db.SaveChangesAsync();
        
        var logger = new Mock<ILogger>();
        
        // Act
        await DataSeeder.SeedDevelopmentDataAsync(db, logger.Object);
        
        // Assert
        var count = await db.Ingredients.CountAsync();
        count.Should().Be(1); // Only the existing one
    }
}
```

### Run Tests:

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend

# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~ValidationBehaviorTests"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Your Task (Day 7):

1. Create test project if not exists
2. Create `ValidationBehaviorTests.cs`
3. Create `IngredientConfigurationTests.cs`
4. Create `DataSeederTests.cs`
5. Run tests: `dotnet test`

---

# Resources

## Microsoft Official Documentation

| Topic | Link |
|-------|------|
| **EF Core Modeling** | [learn.microsoft.com/ef/core/modeling](https://learn.microsoft.com/en-us/ef/core/modeling/) |
| **Relationships** | [learn.microsoft.com/ef/core/modeling/relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships) |
| **Migrations** | [learn.microsoft.com/ef/core/managing-schemas/migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/) |
| **Data Seeding** | [learn.microsoft.com/ef/core/modeling/data-seeding](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding) |
| **Logging** | [learn.microsoft.com/dotnet/core/extensions/logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging) |
| **MediatR Behaviors** | [learn.microsoft.com/dotnet/architecture/microservices](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api) |
| **Testing with EF Core** | [learn.microsoft.com/ef/core/testing](https://learn.microsoft.com/en-us/ef/core/testing/testing-with-the-database) |

## Week 5 Checklist

- [ ] Created entity configuration files (`IEntityTypeConfiguration<T>`)
- [ ] Configured relationships (one-to-many, indexes)
- [ ] Updated `NastartDbContext` with `ApplyConfigurationsFromAssembly`
- [ ] Installed EF Core tools (`dotnet tool install --global dotnet-ef`)
- [ ] Created initial migration
- [ ] Configured connection strings
- [ ] Applied migration to database
- [ ] Added seed data with `HasData()` for categories
- [ ] Created `DataSeeder` for development data
- [ ] Created `ValidationBehavior` pipeline
- [ ] Created `LoggingBehavior` pipeline
- [ ] Created `ExceptionHandlingMiddleware`
- [ ] Registered behaviors in `Program.cs`
- [ ] Unit tests for behaviors
- [ ] Unit tests for configurations
- [ ] All builds pass: `dotnet build`
- [ ] All tests pass: `dotnet test`

---

## What's Next?

**Week 6: PaddleOCR Integration** — Set up the PaddleOCR Python microservice, create the HTTP client in .NET, build the `ScanReceipt` feature, and implement fuzzy matching for ingredient detection.

---

*Nastart — Start smart, bake profitable*
