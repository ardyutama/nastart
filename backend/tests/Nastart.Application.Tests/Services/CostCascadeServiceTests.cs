using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Nastart.Application.Services;
using Nastart.Application.Tests.Infrastructure;
using Nastart.Domain.Entities;
using Nastart.Domain.Enums;
using Nastart.Domain.Common;
using Nastart.Infrastructure.Persistence;

namespace Nastart.Application.Tests.Services;

[TestClass]
public sealed class CostCascadeServiceTests
{
    private static readonly PostgreSqlTestDatabase Database = new();

    [ClassInitialize]
    public static Task ClassInitialize(TestContext _) => Database.InitializeAsync();

    [ClassCleanup]
    public static Task ClassCleanup() => Database.DisposeAsync().AsTask();

    // ─────────────────────────────────────────────────────────────────────────
    // C-2 formula: cost_per_portion = SUM((price / priceHistory.UnitSize) * qty * (1 / yield)) / portions
    // Parameterized to cover multiple formula scenarios in one method.
    // ─────────────────────────────────────────────────────────────────────────
    //   Scenario 1: (10/1000)*500*(1/1.0)/10  = 0.5000
    //   Scenario 2: (5/500)*100*(1/1.0)/4     = 0.2500
    //   Scenario 3: (100/10)*2*(1/0.85)/1     = 23.5294
    public static IEnumerable<object[]> C2FormulaScenarios =>
    [
        // price, unitSize,  qty,   yield,  portions, expectedCostPerPortion
        [10m,    1000m,     500m,   1.0m,   10,       0.5000m],
        [5m,     500m,      100m,   1.0m,   4,        0.2500m],
        [100m,   10m,       2m,     0.85m,  1,        23.5294m],
    ];

    [TestMethod]
    [DynamicData(nameof(C2FormulaScenarios))]
    public async Task RecalculateForIngredientAsync_C2FormulaScenarios_ComputesCorrectCostPerPortion(
        decimal price, decimal unitSize, decimal quantity, decimal yieldPct, int portionCount, decimal expectedCost)
    {
        // Arrange
        await using var db = await Database.CreateDbContextAsync();
        var ingredientId = Guid.NewGuid();
        var recipeId     = Guid.NewGuid();
        var userId       = Guid.NewGuid();
        var unitId       = Guid.NewGuid();

        AddRequiredReferenceData(db, userId, unitId);

        db.Ingredients.Add(new Ingredient
        {
            Id = ingredientId, UserId = userId, Name = "Test Ingredient", UnitId = unitId, UnitSize = unitSize
        });
        db.IngredientPriceHistories.Add(new IngredientPriceHistory
        {
            Id = Guid.NewGuid(), IngredientId = ingredientId,
            Price = price, UnitSize = unitSize, CommittedAt = DateTimeOffset.UtcNow,
            Source = PriceSource.Manual,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        db.Recipes.Add(new Recipe
        {
            Id = recipeId, UserId = userId, Name = "Formula Test Recipe",
            PortionCount = portionCount, CostPerPortion = 0m,
            VersionGroupId = Guid.NewGuid()
        });
        db.RecipeItems.Add(new RecipeItem
        {
            Id = Guid.NewGuid(), RecipeId = recipeId, IngredientId = ingredientId,
            Quantity = quantity, YieldPercentage = yieldPct
        });
        await db.SaveChangesAsync();

        var sut = new CostCascadeService(db, Substitute.For<ILogger<CostCascadeService>>());

        // Act
        var result = await sut.RecalculateForIngredientAsync(ingredientId, CancellationToken.None);

        // Assert
        Assert.AreEqual(1, result.AffectedRecipes);
        Assert.AreEqual(0, result.FailedRecipes);
        var updated = await db.Recipes.FindAsync(recipeId);
        Assert.AreEqual(
            expectedCost,
            updated!.CostPerPortion,
            $"C-2 mismatch: ({price}/{unitSize})*{quantity}*(1/{yieldPct})/{portionCount}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // C-2: multi-ingredient recipe — ALL item costs are summed before dividing by portionCount
    //   Flour:  (10/1000)*500*(1/1.0) = 5.0
    //   Butter: (20/250)*100*(1/1.0)  = 8.0
    //   Total = 13.0 / 4 portions     = 3.2500
    // ─────────────────────────────────────────────────────────────────────────
    [TestMethod]
    public async Task RecalculateForIngredientAsync_WhenMultipleIngredientsInRecipe_SumsAllItemCosts()
    {
        // Arrange
        await using var db = await Database.CreateDbContextAsync();
        var flourId  = Guid.NewGuid();
        var butterId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var userId   = Guid.NewGuid();
        var unitId   = Guid.NewGuid();

        AddRequiredReferenceData(db, userId, unitId);

        db.Ingredients.AddRange(
            new Ingredient { Id = flourId,  UserId = userId, Name = "Flour",  UnitId = unitId, UnitSize = 1000m },
            new Ingredient { Id = butterId, UserId = userId, Name = "Butter", UnitId = unitId, UnitSize = 250m  }
        );
        db.IngredientPriceHistories.AddRange(
            new IngredientPriceHistory
            {
                Id = Guid.NewGuid(), IngredientId = flourId,
                Price = 10m, UnitSize = 1000m, CommittedAt = DateTimeOffset.UtcNow,
                Source = PriceSource.Manual, EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow)
            },
            new IngredientPriceHistory
            {
                Id = Guid.NewGuid(), IngredientId = butterId,
                Price = 20m, UnitSize = 250m, CommittedAt = DateTimeOffset.UtcNow,
                Source = PriceSource.Manual, EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow)
            }
        );
        db.Recipes.Add(new Recipe
        {
            Id = recipeId, UserId = userId, Name = "Butter Cake",
            PortionCount = 4, CostPerPortion = 0m, VersionGroupId = Guid.NewGuid()
        });
        db.RecipeItems.AddRange(
            new RecipeItem
            {
                Id = Guid.NewGuid(), RecipeId = recipeId, IngredientId = flourId,
                Quantity = 500m, YieldPercentage = 1.0m
            },
            new RecipeItem
            {
                Id = Guid.NewGuid(), RecipeId = recipeId, IngredientId = butterId,
                Quantity = 100m, YieldPercentage = 1.0m
            }
        );
        await db.SaveChangesAsync();

        var sut = new CostCascadeService(db, Substitute.For<ILogger<CostCascadeService>>());

        // Act — cascade triggered for flour; butter's price is batch-loaded in the same pass
        var result = await sut.RecalculateForIngredientAsync(flourId, CancellationToken.None);

        // Assert — 5.0 (flour) + 8.0 (butter) = 13.0 / 4 = 3.2500
        Assert.AreEqual(1, result.AffectedRecipes);
        Assert.AreEqual(0, result.FailedRecipes);
        var updated = await db.Recipes.FindAsync(recipeId);
        Assert.AreEqual(3.2500m, updated!.CostPerPortion);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Guard: PortionCount = 0 → CostPerPortion clamped to 0, no DivideByZeroException
    // ─────────────────────────────────────────────────────────────────────────
    [TestMethod]
    public async Task RecalculateForIngredientAsync_WhenPortionCountIsZero_SetsCostToZero()
    {
        // Arrange
        await using var db = await Database.CreateDbContextAsync();
        var ingredientId = Guid.NewGuid();
        var recipeId     = Guid.NewGuid();
        var userId       = Guid.NewGuid();
        var unitId       = Guid.NewGuid();

        AddRequiredReferenceData(db, userId, unitId);

        db.Ingredients.Add(new Ingredient
        {
            Id = ingredientId, UserId = userId, Name = "Flour", UnitId = unitId, UnitSize = 1000m
        });
        db.IngredientPriceHistories.Add(new IngredientPriceHistory
        {
            Id = Guid.NewGuid(), IngredientId = ingredientId,
            Price = 10m, UnitSize = 1000m, CommittedAt = DateTimeOffset.UtcNow,
            Source = PriceSource.Manual, EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        db.Recipes.Add(new Recipe
        {
            Id = recipeId, UserId = userId, Name = "Zero Portion Recipe",
            PortionCount = 0,                    // guard under test
            CostPerPortion = 99m,                // pre-set to non-zero to prove it is overwritten
            VersionGroupId = Guid.NewGuid()
        });
        db.RecipeItems.Add(new RecipeItem
        {
            Id = Guid.NewGuid(), RecipeId = recipeId, IngredientId = ingredientId,
            Quantity = 500m, YieldPercentage = 1.0m
        });
        await db.SaveChangesAsync();

        var sut = new CostCascadeService(db, Substitute.For<ILogger<CostCascadeService>>());

        // Act
        var result = await sut.RecalculateForIngredientAsync(ingredientId, CancellationToken.None);

        // Assert
        Assert.AreEqual(1, result.AffectedRecipes);
        Assert.AreEqual(0, result.FailedRecipes);
        var updated = await db.Recipes.FindAsync(recipeId);
        Assert.AreEqual(0m, updated!.CostPerPortion,
            "PortionCount=0 must clamp CostPerPortion to zero rather than throw DivideByZeroException");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // C-3: no price history → cascade skips the ingredient gracefully
    // ─────────────────────────────────────────────────────────────────────────
    [TestMethod]
    public async Task RecalculateForIngredientAsync_WhenNoPriceHistory_ReturnsZeroCounts()
    {
        // Arrange
        await using var db = await Database.CreateDbContextAsync();
        var sut = new CostCascadeService(db, Substitute.For<ILogger<CostCascadeService>>());

        // Act
        var result = await sut.RecalculateForIngredientAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.AreEqual(0, result.AffectedRecipes);
        Assert.AreEqual(0, result.FailedRecipes);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // C-5: a per-recipe failure writes CascadeErrorLog and does NOT roll back
    //      IngredientPriceHistory; remaining recipes are still processed
    // ─────────────────────────────────────────────────────────────────────────
    [TestMethod]
    public async Task RecalculateForIngredientAsync_WhenOneRecipeFails_LogsErrorAndContinuesOthers()
    {
        // Arrange
        await using var db = await Database.CreateDbContextAsync();
        var ingredientId = Guid.NewGuid();
        var userId       = Guid.NewGuid();
        var unitId       = Guid.NewGuid();

        AddRequiredReferenceData(db, userId, unitId);

        db.Ingredients.Add(new Ingredient
        {
            Id = ingredientId, UserId = userId, Name = "Butter", UnitId = unitId, UnitSize = 500m
        });
        db.IngredientPriceHistories.Add(new IngredientPriceHistory
        {
            Id = Guid.NewGuid(), IngredientId = ingredientId,
            Price = 5m, UnitSize = 500m, CommittedAt = DateTimeOffset.UtcNow,
            Source = PriceSource.Manual, EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });

        // Good recipe — (5/500)*100*(1/1.0)/4 = 0.2500
        var goodRecipeId = Guid.NewGuid();
        db.Recipes.Add(new Recipe
        {
            Id = goodRecipeId, UserId = userId, Name = "Butter Cake",
            PortionCount = 4, CostPerPortion = 0m, VersionGroupId = Guid.NewGuid()
        });
        db.RecipeItems.Add(new RecipeItem
        {
            Id = Guid.NewGuid(), RecipeId = goodRecipeId, IngredientId = ingredientId,
            Quantity = 100m, YieldPercentage = 1.0m
        });

        // Bad recipe — YieldPercentage = 0 causes 1m/0m → DivideByZeroException in C-2 formula
        var badRecipeId = Guid.NewGuid();
        db.Recipes.Add(new Recipe
        {
            Id = badRecipeId, UserId = userId, Name = "Broken Recipe",
            PortionCount = 1, CostPerPortion = 0m, VersionGroupId = Guid.NewGuid()
        });
        db.RecipeItems.Add(new RecipeItem
        {
            Id = Guid.NewGuid(), RecipeId = badRecipeId, IngredientId = ingredientId,
            Quantity = 50m, YieldPercentage = 0m     // zero yield → exception in C-2
        });
        await db.SaveChangesAsync();

        var sut = new CostCascadeService(db, Substitute.For<ILogger<CostCascadeService>>());

        // Act
        var result = await sut.RecalculateForIngredientAsync(ingredientId, CancellationToken.None);

        // Assert — C-5: counts
        Assert.AreEqual(1, result.AffectedRecipes, "One recipe should succeed");
        Assert.AreEqual(1, result.FailedRecipes,   "One recipe should fail");

        var updatedGood = await db.Recipes.FindAsync(goodRecipeId);
        Assert.AreEqual(0.2500m, updatedGood!.CostPerPortion,
            "The successful recipe must be recalculated correctly");

        // C-5: error is written to CascadeErrorLog
        var errorLogged = await db.CascadeErrorLogs.AnyAsync(e => e.RecipeId == badRecipeId);
        Assert.IsTrue(errorLogged, "C-5: CascadeErrorLog must contain an entry for the failed recipe.");

        // C-5: IngredientPriceHistory is append-only — never rolled back on cascade failure
        var priceCount = await db.IngredientPriceHistories
            .CountAsync(p => p.IngredientId == ingredientId);
        Assert.AreEqual(1, priceCount,
            "C-5: IngredientPriceHistory must not be rolled back on cascade failure.");
    }

    private static void AddRequiredReferenceData(AppDbContext db, Guid userId, Guid unitId)
    {
        db.Users.Add(new User
        {
            Id = userId,
            Email = $"user-{userId:N}@example.test",
            PasswordHash = "hashed-password",
            IsEmailVerified = true
        });

        db.Units.Add(new Unit
        {
            Id = unitId,
            Name = $"Unit-{unitId:N}",
            Abbreviation = $"u{unitId:N}"[..20]
        });
    }
}