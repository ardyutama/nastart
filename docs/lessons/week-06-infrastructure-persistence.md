# Week 6: Infrastructure Layer (Persistence) 🗄️

> **Goal**: Build the Infrastructure Layer — DbContext with EF Core, Entity Configurations, Repository implementations, Unit of Work pattern, Domain Event dispatching, and Database Migrations.

---

## Table of Contents
1. [Day 1: DbContext Setup & Configuration](#day-1-dbcontext-setup--configuration)
2. [Day 2: Entity Configurations & Value Objects](#day-2-entity-configurations--value-objects)
3. [Day 3: Strongly-Typed IDs & Value Converters](#day-3-strongly-typed-ids--value-converters)
4. [Day 4: Repository Implementation](#day-4-repository-implementation)
5. [Day 5: Unit of Work & Domain Event Dispatching](#day-5-unit-of-work--domain-event-dispatching)
6. [Day 6: Migrations & Data Seeding](#day-6-migrations--data-seeding)
7. [Day 7: Infrastructure Testing & Dependency Injection](#day-7-infrastructure-testing--dependency-injection)
8. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1: DbContext Setup & Configuration

## 🧒 Explain Like I'm 5

The **DbContext** is like a magic notebook 📓 that remembers everything you tell it:
- "I added a new recipe!"
- "I changed the price of flour!"
- "I deleted an old purchase!"

When you say "Save!", it writes everything to the database at once.

## 🔧 Engineer Language

**DbContext** is EF Core's Unit of Work and Repository combined. It:
- Tracks entity changes
- Manages database connections
- Translates LINQ to SQL
- Handles transactions

> 📖 **Microsoft Docs**: *"A DbContext instance represents a session with the database and can be used to query and save instances of your entities. DbContext is a combination of the Unit Of Work and Repository patterns."*
>
> — [DbContext Class](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.dbcontext)

### Install EF Core Packages

```powershell
cd c:\Users\AU1833\Documents\personal\nastart\backend

# EF Core with PostgreSQL provider
dotnet add src/Nastart.Infrastructure/Nastart.Infrastructure.csproj package Microsoft.EntityFrameworkCore
dotnet add src/Nastart.Infrastructure/Nastart.Infrastructure.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/Nastart.Infrastructure/Nastart.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design

# For API to run migrations
dotnet add src/Nastart.Api/Nastart.Api.csproj package Microsoft.EntityFrameworkCore.Design
```

### Infrastructure Project Structure

```
src/Nastart.Infrastructure/
├── Persistence/
│   ├── NastartDbContext.cs                    # Main DbContext
│   ├── NastartDbContextFactory.cs             # Design-time factory for migrations
│   ├── Configurations/
│   │   ├── Inventory/
│   │   │   ├── IngredientConfiguration.cs
│   │   │   ├── CategoryConfiguration.cs
│   │   │   └── ShopConfiguration.cs
│   │   ├── Recipe/
│   │   │   ├── RecipeConfiguration.cs
│   │   │   └── RecipeItemConfiguration.cs
│   │   ├── Finance/
│   │   │   ├── PurchaseConfiguration.cs
│   │   │   ├── PurchaseItemConfiguration.cs
│   │   │   └── PriceHistoryConfiguration.cs
│   │   └── Alert/
│   │       └── AlertConfiguration.cs
│   ├── Repositories/
│   │   ├── IngredientRepository.cs
│   │   ├── RecipeRepository.cs
│   │   ├── PurchaseRepository.cs
│   │   └── AlertRepository.cs
│   ├── Extensions/
│   │   └── MediatorExtensions.cs              # Domain event dispatching
│   └── Interceptors/
│       ├── AuditableEntityInterceptor.cs      # CreatedAt, ModifiedAt
│       └── DispatchDomainEventsInterceptor.cs # Dispatch events on save
│
├── ExternalServices/
│   ├── PaddleOcrService.cs
│   └── TelegramBotService.cs
│
└── DependencyInjection.cs                     # Service registration
```

### Create NastartDbContext

Create `src/Nastart.Infrastructure/Persistence/NastartDbContext.cs`:

```csharp
using System.Reflection;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Application.Common.Interfaces;
using Nastart.Domain.Alert.Aggregates;
using Nastart.Domain.Common;
using Nastart.Domain.Finance.Aggregates;
using Nastart.Domain.Inventory.Aggregates;
using Nastart.Domain.Recipe.Aggregates;
using Nastart.Infrastructure.Persistence.Extensions;

namespace Nastart.Infrastructure.Persistence;

/// <summary>
/// Main DbContext for Nastart application.
/// Implements IUnitOfWork to support the Unit of Work pattern.
/// </summary>
/// <remarks>
/// Following Microsoft's eShopOnContainers pattern:
/// - DbContext is both Unit of Work and Repository pattern combined
/// - Entity configurations are in separate IEntityTypeConfiguration classes
/// - Domain events are dispatched before SaveChanges
/// 
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design
/// </remarks>
public class NastartDbContext : DbContext, IUnitOfWork
{
    public const string DefaultSchema = "nastart";
    
    private readonly IMediator _mediator;

    public NastartDbContext(
        DbContextOptions<NastartDbContext> options,
        IMediator mediator)
        : base(options)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    #region DbSets — Inventory Context
    
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Shop> Shops => Set<Shop>();
    
    #endregion

    #region DbSets — Recipe Context
    
    public DbSet<Recipe> Recipes => Set<Recipe>();
    
    #endregion

    #region DbSets — Finance Context
    
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    
    #endregion

    #region DbSets — Alert Context
    
    public DbSet<Alert> Alerts => Set<Alert>();
    
    #endregion

    /// <summary>
    /// Configures entity mappings using IEntityTypeConfiguration classes.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Set default schema
        modelBuilder.HasDefaultSchema(DefaultSchema);

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Saves entities and dispatches domain events.
    /// Implements the Unit of Work pattern.
    /// </summary>
    /// <remarks>
    /// Domain events are dispatched BEFORE SaveChanges to ensure:
    /// - Side effects are part of the same transaction
    /// - All handlers use the same DbContext instance
    /// 
    /// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation
    /// </remarks>
    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        // Dispatch domain events before saving
        // This ensures side effects are in the same transaction
        await _mediator.DispatchDomainEventsAsync(this);

        // Update auditable entities
        UpdateAuditableEntities();

        // Save changes
        var result = await base.SaveChangesAsync(cancellationToken);

        return result > 0;
    }

    /// <summary>
    /// Sets CreatedAt and ModifiedAt for auditable entities.
    /// </summary>
    private void UpdateAuditableEntities()
    {
        var entries = ChangeTracker.Entries<IAuditableEntity>();

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.UtcNow;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModifiedAt = DateTime.UtcNow;
            }
        }
    }
}
```

### Create IAuditableEntity Interface

Create `src/Nastart.Domain/Common/IAuditableEntity.cs`:

```csharp
namespace Nastart.Domain.Common;

/// <summary>
/// Interface for entities that track creation and modification timestamps.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    DateTime? ModifiedAt { get; set; }
}
```

### Design-Time DbContext Factory

Create `src/Nastart.Infrastructure/Persistence/NastartDbContextFactory.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Nastart.Infrastructure.Persistence;

/// <summary>
/// Factory for creating DbContext at design time (migrations).
/// Required because our DbContext has constructor dependencies.
/// </summary>
/// <remarks>
/// See: https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation#from-a-design-time-factory
/// </remarks>
public class NastartDbContextFactory : IDesignTimeDbContextFactory<NastartDbContext>
{
    public NastartDbContext CreateDbContext(string[] args)
    {
        // Build configuration from appsettings.json
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<NastartDbContext>();
        
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Host=localhost;Database=nastart;Username=postgres;Password=postgres";
            
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", NastartDbContext.DefaultSchema);
        });

        // For design-time, we use a null mediator wrapper
        return new NastartDbContext(optionsBuilder.Options, new NoOpMediator());
    }
}

/// <summary>
/// No-operation mediator for design-time context creation.
/// </summary>
internal class NoOpMediator : IMediator
{
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        => Task.FromResult<TResponse>(default!);
    
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        => Task.CompletedTask;
    
    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        => Task.FromResult<object?>(null);
    
    public Task Publish(object notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) 
        where TNotification : INotification
        => Task.CompletedTask;
    
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => AsyncEnumerable.Empty<TResponse>();
    
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => AsyncEnumerable.Empty<object?>();
}
```

### Update appsettings.json

Update `src/Nastart.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=nastart;Username=postgres;Password=postgres"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "AllowedHosts": "*"
}
```

---

# Day 2: Entity Configurations & Value Objects

## 🧒 Explain Like I'm 5

When you organize your toys 🧸, you put dolls in one box, cars in another, and blocks in another. Each box has its own rules!

**Entity Configurations** are like instructions for each box:
- "The Recipe box needs a name label"
- "The Ingredient box needs a price sticker"

## 🔧 Engineer Language

**Entity Configurations** use the Fluent API to:
- Map entities to tables
- Configure primary keys, indexes
- Map Value Objects as Owned Entities
- Configure relationships

> 📖 **Microsoft Docs**: *"You can also tell the tools how to create your DbContext by implementing the IDesignTimeDbContextFactory interface."*
>
> — [Implement the infrastructure persistence layer](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core)

### RecipeConfiguration

Create `src/Nastart.Infrastructure/Persistence/Configurations/Recipe/RecipeConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Domain.Recipe.Aggregates;
using Nastart.Domain.Recipe.ValueObjects;

namespace Nastart.Infrastructure.Persistence.Configurations.Recipe;

/// <summary>
/// EF Core configuration for Recipe aggregate root.
/// </summary>
/// <remarks>
/// Key patterns demonstrated:
/// - Strongly-typed ID mapping with value converter
/// - Owned entity for Value Objects (Margin)
/// - Private field mapping for encapsulation
/// - Navigation property configuration
/// 
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core
/// </remarks>
public class RecipeConfiguration : IEntityTypeConfiguration<Domain.Recipe.Aggregates.Recipe>
{
    public void Configure(EntityTypeBuilder<Domain.Recipe.Aggregates.Recipe> builder)
    {
        builder.ToTable("recipes", NastartDbContext.DefaultSchema);

        // Primary key with strongly-typed ID
        builder.HasKey(r => r.Id);
        
        builder.Property(r => r.Id)
            .HasConversion(
                id => id.Value,
                value => new RecipeId(value))
            .HasColumnName("id");

        // Ignore domain events — they're not persisted
        builder.Ignore(r => r.DomainEvents);

        // Name — required, max length
        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("name");

        // Category
        builder.Property(r => r.Category)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("category");

        // Description
        builder.Property(r => r.Description)
            .HasMaxLength(2000)
            .HasColumnName("description");

        // Selling price
        builder.Property(r => r.SellingPrice)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnName("selling_price");

        // Total cost (calculated, but stored for queries)
        builder.Property(r => r.TotalCost)
            .HasPrecision(18, 2)
            .HasColumnName("total_cost");

        // Is active
        builder.Property(r => r.IsActive)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnName("is_active");

        // Margin as owned entity (Value Object)
        builder.OwnsOne(r => r.Margin, marginBuilder =>
        {
            marginBuilder.Property(m => m.Value)
                .HasPrecision(5, 2)
                .HasColumnName("margin_percentage");
        });

        // Auditable properties
        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(r => r.ModifiedAt)
            .HasColumnName("modified_at");

        // Recipe items collection — access via private field
        var navigation = builder.Metadata.FindNavigation(nameof(Domain.Recipe.Aggregates.Recipe.Items));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

        // Index for common queries
        builder.HasIndex(r => r.Category).HasDatabaseName("ix_recipes_category");
        builder.HasIndex(r => r.IsActive).HasDatabaseName("ix_recipes_is_active");
    }
}
```

### RecipeItemConfiguration

Create `src/Nastart.Infrastructure/Persistence/Configurations/Recipe/RecipeItemConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Domain.Inventory.ValueObjects;
using Nastart.Domain.Recipe.Entities;
using Nastart.Domain.Recipe.ValueObjects;

namespace Nastart.Infrastructure.Persistence.Configurations.Recipe;

/// <summary>
/// EF Core configuration for RecipeItem entity.
/// </summary>
public class RecipeItemConfiguration : IEntityTypeConfiguration<RecipeItem>
{
    public void Configure(EntityTypeBuilder<RecipeItem> builder)
    {
        builder.ToTable("recipe_items", NastartDbContext.DefaultSchema);

        // Primary key with strongly-typed ID
        builder.HasKey(ri => ri.Id);
        
        builder.Property(ri => ri.Id)
            .HasConversion(
                id => id.Value,
                value => new RecipeItemId(value))
            .HasColumnName("id");

        // Foreign key to Recipe
        builder.Property<RecipeId>("RecipeId")
            .HasConversion(
                id => id.Value,
                value => new RecipeId(value))
            .HasColumnName("recipe_id");

        // Foreign key to Ingredient (cross-context reference)
        builder.Property(ri => ri.IngredientId)
            .HasConversion(
                id => id.Value,
                value => new IngredientId(value))
            .IsRequired()
            .HasColumnName("ingredient_id");

        // Ingredient name (denormalized for display)
        builder.Property(ri => ri.IngredientName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("ingredient_name");

        // Quantity
        builder.Property(ri => ri.Quantity)
            .IsRequired()
            .HasPrecision(18, 4)
            .HasColumnName("quantity");

        // Unit
        builder.Property(ri => ri.Unit)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName("unit");

        // Unit cost
        builder.Property(ri => ri.UnitCost)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasColumnName("unit_cost");

        // Total cost (calculated)
        builder.Property(ri => ri.TotalCost)
            .HasPrecision(18, 2)
            .HasColumnName("total_cost");

        // Relationship to Recipe
        builder.HasOne<Domain.Recipe.Aggregates.Recipe>()
            .WithMany(r => r.Items)
            .HasForeignKey("RecipeId")
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex("RecipeId", "IngredientId")
            .IsUnique()
            .HasDatabaseName("ix_recipe_items_recipe_ingredient");
    }
}
```

### IngredientConfiguration

Create `src/Nastart.Infrastructure/Persistence/Configurations/Inventory/IngredientConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Domain.Inventory.Aggregates;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>
/// EF Core configuration for Ingredient aggregate root.
/// </summary>
public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.ToTable("ingredients", NastartDbContext.DefaultSchema);

        // Primary key
        builder.HasKey(i => i.Id);
        
        builder.Property(i => i.Id)
            .HasConversion(
                id => id.Value,
                value => new IngredientId(value))
            .HasColumnName("id");

        // Ignore domain events
        builder.Ignore(i => i.DomainEvents);

        // Name
        builder.Property(i => i.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("name");

        // Unit as owned Value Object
        builder.OwnsOne(i => i.Unit, unitBuilder =>
        {
            unitBuilder.Property(u => u.Name)
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnName("unit");
        });

        // Current price as Money Value Object
        builder.OwnsOne(i => i.CurrentPrice, priceBuilder =>
        {
            priceBuilder.Property(p => p.Amount)
                .IsRequired()
                .HasPrecision(18, 2)
                .HasColumnName("current_price");
            
            priceBuilder.Property(p => p.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .HasDefaultValue("IDR")
                .HasColumnName("currency");
        });

        // Stock as Quantity Value Object
        builder.OwnsOne(i => i.Stock, stockBuilder =>
        {
            stockBuilder.Property(s => s.Value)
                .IsRequired()
                .HasPrecision(18, 4)
                .HasColumnName("current_stock");
        });

        // Minimum stock
        builder.Property(i => i.MinimumStock)
            .HasPrecision(18, 4)
            .HasColumnName("minimum_stock");

        // Category foreign key
        builder.Property(i => i.CategoryId)
            .HasConversion(
                id => id.Value,
                value => new CategoryId(value))
            .HasColumnName("category_id");

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Auditable
        builder.Property(i => i.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(i => i.ModifiedAt)
            .HasColumnName("modified_at");

        // Indexes
        builder.HasIndex(i => i.Name).HasDatabaseName("ix_ingredients_name");
        builder.HasIndex(i => i.CategoryId).HasDatabaseName("ix_ingredients_category");
    }
}
```

### CategoryConfiguration

Create `src/Nastart.Infrastructure/Persistence/Configurations/Inventory/CategoryConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Domain.Inventory.Aggregates;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Infrastructure.Persistence.Configurations.Inventory;

/// <summary>
/// EF Core configuration for Category entity.
/// </summary>
public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", NastartDbContext.DefaultSchema);

        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.Id)
            .HasConversion(
                id => id.Value,
                value => new CategoryId(value))
            .HasColumnName("id");

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("name");

        builder.Property(c => c.Description)
            .HasMaxLength(500)
            .HasColumnName("description");

        // Unique index on name
        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasDatabaseName("ix_categories_name");
    }
}
```

### PurchaseConfiguration

Create `src/Nastart.Infrastructure/Persistence/Configurations/Finance/PurchaseConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Domain.Finance.Aggregates;
using Nastart.Domain.Finance.ValueObjects;

namespace Nastart.Infrastructure.Persistence.Configurations.Finance;

/// <summary>
/// EF Core configuration for Purchase aggregate root.
/// </summary>
public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("purchases", NastartDbContext.DefaultSchema);

        builder.HasKey(p => p.Id);
        
        builder.Property(p => p.Id)
            .HasConversion(
                id => id.Value,
                value => new PurchaseId(value))
            .HasColumnName("id");

        builder.Ignore(p => p.DomainEvents);

        // Shop foreign key
        builder.Property(p => p.ShopId)
            .HasConversion(
                id => id.Value,
                value => new ShopId(value))
            .HasColumnName("shop_id");

        builder.Property(p => p.PurchaseDate)
            .IsRequired()
            .HasColumnName("purchase_date");

        // Total as Money Value Object
        builder.OwnsOne(p => p.Total, totalBuilder =>
        {
            totalBuilder.Property(m => m.Amount)
                .HasPrecision(18, 2)
                .HasColumnName("total");
            
            totalBuilder.Property(m => m.Currency)
                .HasMaxLength(3)
                .HasDefaultValue("IDR")
                .HasColumnName("currency");
        });

        builder.Property(p => p.ReceiptImageUrl)
            .HasMaxLength(500)
            .HasColumnName("receipt_image_url");

        builder.Property(p => p.Notes)
            .HasMaxLength(1000)
            .HasColumnName("notes");

        // Auditable
        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        // Items navigation
        var navigation = builder.Metadata.FindNavigation(nameof(Purchase.Items));
        navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

        // Indexes
        builder.HasIndex(p => p.PurchaseDate).HasDatabaseName("ix_purchases_date");
        builder.HasIndex(p => p.ShopId).HasDatabaseName("ix_purchases_shop");
    }
}
```

---

# Day 3: Strongly-Typed IDs & Value Converters

## 🧒 Explain Like I'm 5

Imagine you have a special key 🔑 that only opens your toy box, and your friend has a different key that only opens their toy box. You can't mix them up!

**Strongly-typed IDs** are like those special keys — a RecipeId can't be confused with an IngredientId, even though both are just numbers inside.

## 🔧 Engineer Language

**Value Converters** in EF Core transform values between the domain model and database:
- `RecipeId` (domain) ↔ `Guid` (database)
- `Money` (domain) ↔ `decimal` (database)

> 📖 **Microsoft Docs**: *"Value conversions can be configured to map domain model types to database provider types."*
>
> — [Value Conversions](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions)

### Generic ValueConverter for Strongly-Typed IDs

Create `src/Nastart.Infrastructure/Persistence/Converters/StronglyTypedIdConverter.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Nastart.Infrastructure.Persistence.Converters;

/// <summary>
/// Generic value converter for strongly-typed IDs that wrap a Guid.
/// </summary>
/// <typeparam name="TId">The strongly-typed ID type.</typeparam>
public class StronglyTypedIdConverter<TId> : ValueConverter<TId, Guid>
    where TId : class
{
    public StronglyTypedIdConverter()
        : base(
            id => GetGuidValue(id),
            value => CreateId(value))
    {
    }

    private static Guid GetGuidValue(TId id)
    {
        var property = typeof(TId).GetProperty("Value");
        return (Guid)(property?.GetValue(id) ?? Guid.Empty);
    }

    private static TId CreateId(Guid value)
    {
        return (TId)Activator.CreateInstance(typeof(TId), value)!;
    }
}
```

### Convention for Auto-Configuring Strongly-Typed IDs

Create `src/Nastart.Infrastructure/Persistence/Conventions/StronglyTypedIdConvention.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Nastart.Domain.Common;

namespace Nastart.Infrastructure.Persistence.Conventions;

/// <summary>
/// Convention to automatically configure strongly-typed IDs.
/// Applies HasConversion for any property implementing IStronglyTypedId.
/// </summary>
public static class StronglyTypedIdConventions
{
    /// <summary>
    /// Configures all strongly-typed IDs in the model.
    /// Call this in OnModelCreating after ApplyConfigurationsFromAssembly.
    /// </summary>
    public static void ConfigureStronglyTypedIds(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                var clrType = property.ClrType;
                
                // Check if the type is a strongly-typed ID (has Value property of type Guid)
                if (IsStronglyTypedId(clrType))
                {
                    var converterType = typeof(StronglyTypedIdConverter<>).MakeGenericType(clrType);
                    var converter = Activator.CreateInstance(converterType);
                    
                    property.SetValueConverter((Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter?)converter);
                }
            }
        }
    }

    private static bool IsStronglyTypedId(Type type)
    {
        if (type.IsValueType || type == typeof(string)) return false;
        
        var valueProperty = type.GetProperty("Value");
        return valueProperty?.PropertyType == typeof(Guid);
    }
}
```

### Money Value Object Converter

Create `src/Nastart.Infrastructure/Persistence/Converters/MoneyConverter.cs`:

```csharp
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Nastart.Domain.Common.ValueObjects;

namespace Nastart.Infrastructure.Persistence.Converters;

/// <summary>
/// Value converter for Money value object.
/// Stores as decimal, uses default currency.
/// </summary>
public class MoneyConverter : ValueConverter<Money, decimal>
{
    public MoneyConverter()
        : base(
            money => money.Amount,
            amount => new Money(amount))
    {
    }
}

/// <summary>
/// Comparer for Money value object.
/// Required for change tracking.
/// </summary>
public class MoneyComparer : Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Money>
{
    public MoneyComparer()
        : base(
            (m1, m2) => m1 != null && m2 != null && m1.Amount == m2.Amount && m1.Currency == m2.Currency,
            m => m.GetHashCode())
    {
    }
}
```

### Configuring Value Objects as Owned Types

Owned entity types are perfect for Value Objects:

```csharp
// In IngredientConfiguration.cs

// Unit as owned Value Object
builder.OwnsOne(i => i.Unit, unitBuilder =>
{
    unitBuilder.Property(u => u.Name)
        .IsRequired()
        .HasMaxLength(20)
        .HasColumnName("unit");
});

// Money as owned Value Object with multiple properties
builder.OwnsOne(i => i.CurrentPrice, priceBuilder =>
{
    priceBuilder.Property(p => p.Amount)
        .IsRequired()
        .HasPrecision(18, 2)
        .HasColumnName("current_price");
    
    priceBuilder.Property(p => p.Currency)
        .IsRequired()
        .HasMaxLength(3)
        .HasDefaultValue("IDR")
        .HasColumnName("currency");
});
```

> 📖 **Microsoft Docs**: *"Address value object persisted as owned entity type supported since EF Core 2.0"*
>
> — [Implement value objects](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/implement-value-objects)

### Nested Owned Types

For complex Value Objects with nested properties:

```csharp
// Example: OrderDetails with BillingAddress and ShippingAddress
orderConfiguration.OwnsOne(p => p.OrderDetails, cb =>
{
    cb.OwnsOne(c => c.BillingAddress);
    cb.OwnsOne(c => c.ShippingAddress);
});
```

---

# Day 4: Repository Implementation

## 🧒 Explain Like I'm 5

A **repository** is like a librarian 📚. You don't go into the back room to find books yourself — you ask the librarian:
- "Can I have the recipe with this name?"
- "Please add this new ingredient to the shelf!"

## 🔧 Engineer Language

**Repositories** provide collection-like interfaces for aggregates:
- Abstract away database details
- Work only with aggregate roots
- Return domain entities, not DTOs

> 📖 **Microsoft Docs**: *"Use a Repository to decouple the domain model from query logic...Repositories are for aggregate roots, not for any entity."*
>
> — [The Repository pattern](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)

### IRepository Interface (Domain Layer)

Create `src/Nastart.Domain/Common/IRepository.cs`:

```csharp
namespace Nastart.Domain.Common;

/// <summary>
/// Base repository interface for aggregate roots.
/// Provides access to the Unit of Work for saving.
/// </summary>
/// <typeparam name="T">The aggregate root type.</typeparam>
/// <remarks>
/// Repositories are ONLY for aggregate roots, not for any entity.
/// 
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design
/// </remarks>
public interface IRepository<T> where T : AggregateRoot
{
    IUnitOfWork UnitOfWork { get; }
}
```

### IUnitOfWork Interface (Domain Layer)

Create `src/Nastart.Domain/Common/IUnitOfWork.cs`:

```csharp
namespace Nastart.Domain.Common;

/// <summary>
/// Unit of Work interface for coordinating changes across repositories.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Saves all changes and dispatches domain events.
    /// </summary>
    Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default);
}
```

### IRecipeRepository Interface

Create `src/Nastart.Domain/Recipe/IRecipeRepository.cs`:

```csharp
using Nastart.Domain.Common;
using Nastart.Domain.Inventory.ValueObjects;
using Nastart.Domain.Recipe.Aggregates;
using Nastart.Domain.Recipe.ValueObjects;

namespace Nastart.Domain.Recipe;

/// <summary>
/// Repository interface for Recipe aggregate.
/// </summary>
public interface IRecipeRepository : IRepository<Aggregates.Recipe>
{
    Task<Aggregates.Recipe?> GetByIdAsync(RecipeId id, CancellationToken cancellationToken = default);
    
    Task<Aggregates.Recipe?> GetByIdWithItemsAsync(RecipeId id, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<Aggregates.Recipe>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<Aggregates.Recipe>> GetByIngredientIdAsync(IngredientId ingredientId, CancellationToken cancellationToken = default);
    
    Aggregates.Recipe Add(Aggregates.Recipe recipe);
    
    void Update(Aggregates.Recipe recipe);
    
    void Delete(Aggregates.Recipe recipe);
}
```

### RecipeRepository Implementation

Create `src/Nastart.Infrastructure/Persistence/Repositories/RecipeRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Nastart.Domain.Common;
using Nastart.Domain.Inventory.ValueObjects;
using Nastart.Domain.Recipe;
using Nastart.Domain.Recipe.Aggregates;
using Nastart.Domain.Recipe.ValueObjects;

namespace Nastart.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IRecipeRepository.
/// </summary>
/// <remarks>
/// Following Microsoft's eShopOnContainers pattern:
/// - Repository exposes IUnitOfWork for coordinated saves
/// - Uses DbContext directly (no generic repository)
/// - Includes eager loading for aggregates
/// 
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core
/// </remarks>
public class RecipeRepository : IRecipeRepository
{
    private readonly NastartDbContext _context;

    public RecipeRepository(NastartDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IUnitOfWork UnitOfWork => _context;

    public async Task<Recipe?> GetByIdAsync(RecipeId id, CancellationToken cancellationToken = default)
    {
        return await _context.Recipes
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Recipe?> GetByIdWithItemsAsync(RecipeId id, CancellationToken cancellationToken = default)
    {
        return await _context.Recipes
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Recipe>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Recipes
            .Where(r => r.IsActive)
            .Include(r => r.Items)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Recipe>> GetByIngredientIdAsync(
        IngredientId ingredientId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Recipes
            .Include(r => r.Items)
            .Where(r => r.Items.Any(i => i.IngredientId == ingredientId))
            .ToListAsync(cancellationToken);
    }

    public Recipe Add(Recipe recipe)
    {
        return _context.Recipes.Add(recipe).Entity;
    }

    public void Update(Recipe recipe)
    {
        _context.Entry(recipe).State = EntityState.Modified;
    }

    public void Delete(Recipe recipe)
    {
        _context.Recipes.Remove(recipe);
    }
}
```

### IngredientRepository Implementation

Create `src/Nastart.Infrastructure/Persistence/Repositories/IngredientRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Nastart.Domain.Common;
using Nastart.Domain.Inventory;
using Nastart.Domain.Inventory.Aggregates;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IIngredientRepository.
/// </summary>
public class IngredientRepository : IIngredientRepository
{
    private readonly NastartDbContext _context;

    public IngredientRepository(NastartDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IUnitOfWork UnitOfWork => _context;

    public async Task<Ingredient?> GetByIdAsync(IngredientId id, CancellationToken cancellationToken = default)
    {
        return await _context.Ingredients
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<Ingredient?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _context.Ingredients
            .FirstOrDefaultAsync(i => i.Name.ToLower() == name.ToLower(), cancellationToken);
    }

    public async Task<IReadOnlyList<Ingredient>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Ingredients
            .OrderBy(i => i.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Ingredient>> GetLowStockAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Ingredients
            .Where(i => i.Stock.Value <= i.MinimumStock)
            .OrderBy(i => i.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Ingredient>> GetByCategoryAsync(
        CategoryId categoryId, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Ingredients
            .Where(i => i.CategoryId == categoryId)
            .OrderBy(i => i.Name)
            .ToListAsync(cancellationToken);
    }

    public Ingredient Add(Ingredient ingredient)
    {
        return _context.Ingredients.Add(ingredient).Entity;
    }

    public void Update(Ingredient ingredient)
    {
        _context.Entry(ingredient).State = EntityState.Modified;
    }

    public void Delete(Ingredient ingredient)
    {
        _context.Ingredients.Remove(ingredient);
    }
}
```

### PurchaseRepository Implementation

Create `src/Nastart.Infrastructure/Persistence/Repositories/PurchaseRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Nastart.Domain.Common;
using Nastart.Domain.Finance;
using Nastart.Domain.Finance.Aggregates;
using Nastart.Domain.Finance.ValueObjects;

namespace Nastart.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IPurchaseRepository.
/// </summary>
public class PurchaseRepository : IPurchaseRepository
{
    private readonly NastartDbContext _context;

    public PurchaseRepository(NastartDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IUnitOfWork UnitOfWork => _context;

    public async Task<Purchase?> GetByIdAsync(PurchaseId id, CancellationToken cancellationToken = default)
    {
        return await _context.Purchases
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Purchase>> GetByDateRangeAsync(
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Purchases
            .Include(p => p.Items)
            .Where(p => p.PurchaseDate >= startDate && p.PurchaseDate <= endDate)
            .OrderByDescending(p => p.PurchaseDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Purchase>> GetRecentAsync(
        int count, 
        CancellationToken cancellationToken = default)
    {
        return await _context.Purchases
            .Include(p => p.Items)
            .OrderByDescending(p => p.PurchaseDate)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public Purchase Add(Purchase purchase)
    {
        return _context.Purchases.Add(purchase).Entity;
    }

    public void Update(Purchase purchase)
    {
        _context.Entry(purchase).State = EntityState.Modified;
    }

    public void Delete(Purchase purchase)
    {
        _context.Purchases.Remove(purchase);
    }
}
```

---

# Day 5: Unit of Work & Domain Event Dispatching

## 🧒 Explain Like I'm 5

When you're building with blocks 🧱, you don't want to save after every single block — you wait until your tower is finished, then take a photo!

**Unit of Work** is like waiting until everything is ready, then saving all at once. If something breaks, you can start over!

**Domain Events** are like announcements 📣: "The tower is built!" — and everyone who wants to know gets told.

## 🔧 Engineer Language

**Unit of Work** coordinates:
- Multiple repository changes in one transaction
- Domain event dispatching
- Rollback on failure

> 📖 **Microsoft Docs**: *"Data persistence components provide access to the data hosted within the boundaries of a microservice. They contain the actual implementation of components such as repositories and Unit of Work classes."*
>
> — [Design the infrastructure persistence layer](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design)

### MediatorExtensions for Domain Event Dispatching

Create `src/Nastart.Infrastructure/Persistence/Extensions/MediatorExtensions.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Domain.Common;

namespace Nastart.Infrastructure.Persistence.Extensions;

/// <summary>
/// Extension methods for dispatching domain events via MediatR.
/// </summary>
/// <remarks>
/// Domain events are dispatched BEFORE SaveChanges to ensure:
/// - All side effects are part of the same transaction
/// - Handlers can modify entities before commit
/// - Failure in any handler rolls back everything
/// 
/// See: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation
/// </remarks>
public static class MediatorExtensions
{
    /// <summary>
    /// Dispatches all domain events from tracked entities.
    /// </summary>
    public static async Task DispatchDomainEventsAsync(this IMediator mediator, DbContext context)
    {
        // Get all entities with domain events
        var domainEntities = context.ChangeTracker
            .Entries<Entity>()
            .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Any())
            .ToList();

        // Collect all events
        var domainEvents = domainEntities
            .SelectMany(x => x.Entity.DomainEvents)
            .ToList();

        // Clear events from entities BEFORE dispatching
        // This prevents infinite loops if handlers modify entities
        domainEntities.ForEach(entity => entity.Entity.ClearDomainEvents());

        // Dispatch each event
        foreach (var domainEvent in domainEvents)
        {
            await mediator.Publish(domainEvent);
        }
    }
}
```

### Update Entity Base Class

Update `src/Nastart.Domain/Common/Entity.cs`:

```csharp
using MediatR;

namespace Nastart.Domain.Common;

/// <summary>
/// Base class for all entities.
/// Provides identity comparison and domain event support.
/// </summary>
public abstract class Entity
{
    private List<INotification>? _domainEvents;

    /// <summary>
    /// Domain events raised by this entity.
    /// </summary>
    public IReadOnlyCollection<INotification> DomainEvents 
        => _domainEvents?.AsReadOnly() ?? Array.Empty<INotification>().ToList().AsReadOnly();

    /// <summary>
    /// Adds a domain event to be dispatched on save.
    /// </summary>
    public void AddDomainEvent(INotification eventItem)
    {
        _domainEvents ??= new List<INotification>();
        _domainEvents.Add(eventItem);
    }

    /// <summary>
    /// Removes a domain event.
    /// </summary>
    public void RemoveDomainEvent(INotification eventItem)
    {
        _domainEvents?.Remove(eventItem);
    }

    /// <summary>
    /// Clears all domain events.
    /// Called after events are dispatched.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents?.Clear();
    }

    /// <summary>
    /// Entity equality is based on ID, not reference.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return false; // Override in derived classes with ID comparison
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    public static bool operator ==(Entity? left, Entity? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(Entity? left, Entity? right)
    {
        return !(left == right);
    }
}
```

### Domain Event Example: Raising in Aggregate

```csharp
// In Recipe.cs aggregate
public void RecalculateCost()
{
    var previousMargin = Margin;
    
    TotalCost = Items.Sum(i => i.TotalCost);
    Margin = CalculateMargin(SellingPrice, TotalCost);

    // Check if margin dropped below threshold
    if (Margin.Value < MinimumMarginThreshold && 
        (previousMargin is null || previousMargin.Value >= MinimumMarginThreshold))
    {
        // Raise domain event
        AddDomainEvent(new MarginBelowThresholdEvent(
            RecipeId: Id,
            RecipeName: Name,
            CurrentMarginPercentage: Margin.Value,
            ThresholdPercentage: MinimumMarginThreshold
        ));
    }
}
```

### Transaction with Domain Events Flow

```
1. Command Handler calls aggregate methods
   └─> Aggregate raises domain events (stores in _domainEvents)

2. Command Handler calls repository.Add/Update

3. Command Handler calls unitOfWork.SaveEntitiesAsync()
   └─> DbContext.SaveEntitiesAsync()
       ├─> mediator.DispatchDomainEventsAsync(this)
       │   ├─> Collect events from tracked entities
       │   ├─> Clear events from entities
       │   └─> Publish each event (handlers run)
       │       └─> Handlers can modify other entities
       └─> base.SaveChangesAsync()
           └─> All changes committed in one transaction
```

### Handling Failures

If any domain event handler throws:
- Transaction is rolled back
- No changes are saved
- Exception propagates to command handler
- Command handler can return failure result

---

# Day 6: Migrations & Data Seeding

## 🧒 Explain Like I'm 5

When you get new furniture 🪑, someone has to set it up. **Migrations** are like instruction booklets that tell the database how to set up new tables and columns.

**Data seeding** is like putting starter toys in your toy box when you first get it!

## 🔧 Engineer Language

**Migrations** are versioned schema changes:
- Generated from model changes
- Applied incrementally
- Can be rolled back

**Data Seeding** populates initial/reference data:
- `HasData()` for static data (categories)
- `UseSeeding()` for dynamic data (EF Core 9+)

> 📖 **Microsoft Docs**: *"Data seeding is the process of populating a database with an initial set of data."*
>
> — [Data Seeding](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding)

### Creating the Initial Migration

```powershell
cd c:\Users\AU1833\Documents\personal\nastart\backend

# Add migration
dotnet ef migrations add InitialCreate `
    --project src/Nastart.Infrastructure `
    --startup-project src/Nastart.Api `
    --output-dir Persistence/Migrations

# Apply migration
dotnet ef database update `
    --project src/Nastart.Infrastructure `
    --startup-project src/Nastart.Api
```

### Seeding Categories with HasData

Update `src/Nastart.Infrastructure/Persistence/Configurations/Inventory/CategoryConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Domain.Inventory.Aggregates;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Infrastructure.Persistence.Configurations.Inventory;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", NastartDbContext.DefaultSchema);

        builder.HasKey(c => c.Id);
        
        builder.Property(c => c.Id)
            .HasConversion(
                id => id.Value,
                value => new CategoryId(value))
            .HasColumnName("id");

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("name");

        builder.Property(c => c.Description)
            .HasMaxLength(500)
            .HasColumnName("description");

        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasDatabaseName("ix_categories_name");

        // Seed initial categories
        builder.HasData(
            CreateCategory("11111111-1111-1111-1111-111111111001", "Flour & Grains", "All types of flour and grain products"),
            CreateCategory("11111111-1111-1111-1111-111111111002", "Sugar & Sweeteners", "Sugar, honey, syrups"),
            CreateCategory("11111111-1111-1111-1111-111111111003", "Dairy", "Milk, butter, cream, cheese"),
            CreateCategory("11111111-1111-1111-1111-111111111004", "Eggs", "Chicken eggs, duck eggs"),
            CreateCategory("11111111-1111-1111-1111-111111111005", "Fats & Oils", "Butter, margarine, cooking oils"),
            CreateCategory("11111111-1111-1111-1111-111111111006", "Leavening", "Yeast, baking powder, baking soda"),
            CreateCategory("11111111-1111-1111-1111-111111111007", "Chocolate & Cocoa", "Chocolate, cocoa powder"),
            CreateCategory("11111111-1111-1111-1111-111111111008", "Fruits & Nuts", "Fresh, dried, canned fruits and nuts"),
            CreateCategory("11111111-1111-1111-1111-111111111009", "Flavorings", "Vanilla, extracts, spices"),
            CreateCategory("11111111-1111-1111-1111-111111111010", "Packaging", "Boxes, bags, containers")
        );
    }

    private static object CreateCategory(string id, string name, string description)
    {
        return new
        {
            Id = Guid.Parse(id),
            Name = name,
            Description = description
        };
    }
}
```

### Seeding Shops

Update `src/Nastart.Infrastructure/Persistence/Configurations/Inventory/ShopConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nastart.Domain.Inventory.Aggregates;
using Nastart.Domain.Inventory.ValueObjects;

namespace Nastart.Infrastructure.Persistence.Configurations.Inventory;

public class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.ToTable("shops", NastartDbContext.DefaultSchema);

        builder.HasKey(s => s.Id);
        
        builder.Property(s => s.Id)
            .HasConversion(
                id => id.Value,
                value => new ShopId(value))
            .HasColumnName("id");

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnName("name");

        builder.Property(s => s.Address)
            .HasMaxLength(500)
            .HasColumnName("address");

        builder.HasIndex(s => s.Name)
            .HasDatabaseName("ix_shops_name");

        // Seed common shops
        builder.HasData(
            new { Id = Guid.Parse("22222222-2222-2222-2222-222222222001"), Name = "TBK (Toko Bahan Kue)", Address = (string?)null },
            new { Id = Guid.Parse("22222222-2222-2222-2222-222222222002"), Name = "Indomaret", Address = (string?)null },
            new { Id = Guid.Parse("22222222-2222-2222-2222-222222222003"), Name = "Alfamart", Address = (string?)null },
            new { Id = Guid.Parse("22222222-2222-2222-2222-222222222004"), Name = "Superindo", Address = (string?)null },
            new { Id = Guid.Parse("22222222-2222-2222-2222-222222222005"), Name = "Traditional Market", Address = (string?)null }
        );
    }
}
```

### UseSeeding for Dynamic Data (EF Core 9+)

In `Program.cs` or `DependencyInjection.cs`:

```csharp
// In DependencyInjection.cs
public static IServiceCollection AddInfrastructureServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");

    services.AddDbContext<NastartDbContext>((sp, options) =>
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", NastartDbContext.DefaultSchema);
        });

        // EF Core 9+ seeding
        options.UseSeeding((context, _) =>
        {
            // Dynamic seeding logic
            var dbContext = (NastartDbContext)context;
            
            // Example: Seed demo user data
            // This runs every time but only adds if not exists
        });

        options.UseAsyncSeeding(async (context, _, cancellationToken) =>
        {
            // Async seeding
        });
    });

    return services;
}
```

### Migration Commands Reference

```powershell
# Add a new migration
dotnet ef migrations add [MigrationName] `
    --project src/Nastart.Infrastructure `
    --startup-project src/Nastart.Api

# Update database to latest migration
dotnet ef database update `
    --project src/Nastart.Infrastructure `
    --startup-project src/Nastart.Api

# Generate SQL script (for production)
dotnet ef migrations script `
    --project src/Nastart.Infrastructure `
    --startup-project src/Nastart.Api `
    --output migrations.sql

# Rollback to specific migration
dotnet ef database update [MigrationName] `
    --project src/Nastart.Infrastructure `
    --startup-project src/Nastart.Api

# Remove last migration (if not applied)
dotnet ef migrations remove `
    --project src/Nastart.Infrastructure `
    --startup-project src/Nastart.Api

# List all migrations
dotnet ef migrations list `
    --project src/Nastart.Infrastructure `
    --startup-project src/Nastart.Api
```

---

# Day 7: Infrastructure Testing & Dependency Injection

## 🧒 Explain Like I'm 5

Before going on a trip 🚗, you check if everything works:
- Does the car start?
- Do the lights work?
- Is there gas?

**Infrastructure tests** check if the database stuff works correctly!

## 🔧 Engineer Language

**Integration tests** for Infrastructure verify:
- Repositories correctly save/load entities
- Configurations map Value Objects properly
- Domain events are dispatched
- Transactions work correctly

### DependencyInjection for Infrastructure

Create `src/Nastart.Infrastructure/DependencyInjection.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nastart.Application.Common.Interfaces;
using Nastart.Domain.Common;
using Nastart.Domain.Finance;
using Nastart.Domain.Inventory;
using Nastart.Domain.Recipe;
using Nastart.Infrastructure.Persistence;
using Nastart.Infrastructure.Persistence.Repositories;

namespace Nastart.Infrastructure;

/// <summary>
/// Extension methods for registering Infrastructure services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // Register DbContext
        services.AddDbContext<NastartDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable(
                    "__EFMigrationsHistory", 
                    NastartDbContext.DefaultSchema);
                
                // Enable retry on failure
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            });

            // Enable sensitive data logging in development
            #if DEBUG
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
            #endif
        });

        // Register IUnitOfWork
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<NastartDbContext>());

        // Register repositories
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<IIngredientRepository, IngredientRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();

        // Register query services
        services.AddScoped<IRecipeQueries, RecipeQueries>();

        return services;
    }
}
```

### Update Program.cs

Update `src/Nastart.Api/Program.cs`:

```csharp
using Nastart.Application;
using Nastart.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Testing with In-Memory Database

Create test helper `tests/Nastart.Infrastructure.Tests/TestDbContextFactory.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Nastart.Infrastructure.Persistence;

namespace Nastart.Infrastructure.Tests;

/// <summary>
/// Factory for creating in-memory DbContext for tests.
/// </summary>
public static class TestDbContextFactory
{
    public static NastartDbContext Create()
    {
        var options = new DbContextOptionsBuilder<NastartDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mediator = Substitute.For<IMediator>();
        
        var context = new NastartDbContext(options, mediator);
        context.Database.EnsureCreated();

        return context;
    }
}
```

### Repository Integration Tests

Create `tests/Nastart.Infrastructure.Tests/Repositories/RecipeRepositoryTests.cs`:

```csharp
using FluentAssertions;
using Nastart.Domain.Recipe.Aggregates;
using Nastart.Domain.Recipe.ValueObjects;
using Nastart.Infrastructure.Persistence.Repositories;

namespace Nastart.Infrastructure.Tests.Repositories;

public class RecipeRepositoryTests : IDisposable
{
    private readonly NastartDbContext _context;
    private readonly RecipeRepository _repository;

    public RecipeRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _repository = new RecipeRepository(_context);
    }

    [Fact]
    public async Task Add_ShouldPersistRecipe()
    {
        // Arrange
        var recipe = Recipe.Create(
            name: "Test Recipe",
            sellingPrice: 25000m,
            category: "Dessert"
        );

        // Act
        _repository.Add(recipe);
        await _context.SaveChangesAsync();

        // Assert
        var saved = await _repository.GetByIdAsync(recipe.Id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Test Recipe");
        saved.SellingPrice.Should().Be(25000m);
    }

    [Fact]
    public async Task GetByIdWithItemsAsync_ShouldLoadItems()
    {
        // Arrange
        var recipe = Recipe.Create(
            name: "Recipe with Items",
            sellingPrice: 30000m,
            category: "Cake"
        );

        recipe.AddIngredient(
            ingredientId: new IngredientId(Guid.NewGuid()),
            ingredientName: "Flour",
            quantity: 0.5m,
            unit: "kg",
            unitCost: 15000m
        );

        _repository.Add(recipe);
        await _context.SaveChangesAsync();

        // Clear change tracker to force fresh load
        _context.ChangeTracker.Clear();

        // Act
        var loaded = await _repository.GetByIdWithItemsAsync(recipe.Id);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Items.Should().HaveCount(1);
        loaded.Items.First().IngredientName.Should().Be("Flour");
    }

    [Fact]
    public async Task GetAllActiveAsync_ShouldReturnOnlyActiveRecipes()
    {
        // Arrange
        var active1 = Recipe.Create("Active 1", 10000m, "Cat");
        var active2 = Recipe.Create("Active 2", 20000m, "Cat");
        var inactive = Recipe.Create("Inactive", 30000m, "Cat");
        inactive.Deactivate();

        _repository.Add(active1);
        _repository.Add(active2);
        _repository.Add(inactive);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        // Act
        var activeRecipes = await _repository.GetAllActiveAsync();

        // Assert
        activeRecipes.Should().HaveCount(2);
        activeRecipes.Should().AllSatisfy(r => r.IsActive.Should().BeTrue());
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
```

### Testing Value Object Mapping

```csharp
[Fact]
public async Task ShouldPersistValueObjects()
{
    // Arrange
    var ingredient = Ingredient.Create(
        name: "Test Flour",
        unit: new Unit("kg"),
        currentPrice: new Money(25000m, "IDR"),
        stock: new Quantity(10m),
        minimumStock: 2m,
        categoryId: new CategoryId(Guid.NewGuid())
    );

    _context.Ingredients.Add(ingredient);
    await _context.SaveChangesAsync();
    _context.ChangeTracker.Clear();

    // Act
    var loaded = await _context.Ingredients.FirstAsync();

    // Assert
    loaded.Unit.Name.Should().Be("kg");
    loaded.CurrentPrice.Amount.Should().Be(25000m);
    loaded.CurrentPrice.Currency.Should().Be("IDR");
    loaded.Stock.Value.Should().Be(10m);
}
```

---

# Resources

## Microsoft Official Documentation (Verified)

| Topic | Link |
|-------|------|
| Design the infrastructure persistence layer | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design |
| Implement the infrastructure persistence layer | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core |
| Domain events: Design and implementation | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation |
| Value Conversions | https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions |
| Owned Entity Types | https://learn.microsoft.com/en-us/ef/core/modeling/owned-entities |
| Data Seeding | https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding |
| DbContext Lifetime, Configuration, and Initialization | https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/ |

## Folder Structure After Week 6

```
src/Nastart.Infrastructure/
├── Persistence/
│   ├── NastartDbContext.cs                        ✅
│   ├── NastartDbContextFactory.cs                 ✅
│   ├── Configurations/
│   │   ├── Inventory/
│   │   │   ├── IngredientConfiguration.cs         ✅
│   │   │   ├── CategoryConfiguration.cs           ✅
│   │   │   └── ShopConfiguration.cs               ✅
│   │   ├── Recipe/
│   │   │   ├── RecipeConfiguration.cs             ✅
│   │   │   └── RecipeItemConfiguration.cs         ✅
│   │   └── Finance/
│   │       ├── PurchaseConfiguration.cs           ✅
│   │       └── PurchaseItemConfiguration.cs       ✅
│   ├── Repositories/
│   │   ├── RecipeRepository.cs                    ✅
│   │   ├── IngredientRepository.cs                ✅
│   │   ├── PurchaseRepository.cs                  ✅
│   │   └── AlertRepository.cs                     ✅
│   ├── Extensions/
│   │   └── MediatorExtensions.cs                  ✅
│   ├── Converters/
│   │   ├── StronglyTypedIdConverter.cs            ✅
│   │   └── MoneyConverter.cs                      ✅
│   └── Migrations/
│       └── [Generated migrations]                 ✅
│
└── DependencyInjection.cs                         ✅

src/Nastart.Domain/
├── Common/
│   ├── Entity.cs                                  ✅ (updated)
│   ├── AggregateRoot.cs                           ✅
│   ├── IRepository.cs                             ✅
│   ├── IUnitOfWork.cs                             ✅
│   └── IAuditableEntity.cs                        ✅

tests/Nastart.Infrastructure.Tests/
├── Repositories/
│   ├── RecipeRepositoryTests.cs                   ✅
│   └── IngredientRepositoryTests.cs               ✅
└── TestDbContextFactory.cs                        ✅
```

## Week 6 Checklist

- [x] Install EF Core packages (Npgsql, Design)
- [x] Create `NastartDbContext` implementing `IUnitOfWork`
- [x] Create `NastartDbContextFactory` for design-time migrations
- [x] Create Entity Configurations with `IEntityTypeConfiguration<T>`
- [x] Configure Value Objects with `OwnsOne()` / `OwnsMany()`
- [x] Create Value Converters for strongly-typed IDs
- [x] Implement Repositories: `RecipeRepository`, `IngredientRepository`, `PurchaseRepository`
- [x] Implement Domain Event dispatching via `MediatorExtensions`
- [x] Create initial migration and seed data
- [x] Create `DependencyInjection.cs` for Infrastructure services
- [x] Write repository integration tests

---

## Next Week Preview: Week 7 — API Layer + PaddleOCR Integration

In Week 7, we'll build the API Layer:
- **Controllers**: Using MediatR to dispatch commands/queries
- **Minimal APIs**: Alternative to controllers
- **Error Handling**: Global exception middleware
- **API Versioning**: Managing API versions
- **PaddleOCR**: Python microservice integration for receipt scanning

---

*Last Updated: Week 6 Lesson*
*Stack: .NET 10, EF Core 10, PostgreSQL 18, MediatR*
