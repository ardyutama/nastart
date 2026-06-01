using ErrorOr;
using Nastart.Application.Features.Recipes.Queries.GetRecipeById;
using Nastart.Application.Tests.Infrastructure;
using Nastart.Domain.Common;
using Nastart.Domain.Entities;
using Nastart.Domain.Enums;

namespace Nastart.Application.Tests.Features.Recipes;

[TestClass]
public sealed class GetRecipeByIdHandlerTests
{
    private static readonly PostgreSqlTestDatabase Database = new();

    [ClassInitialize]
    public static Task ClassInitialize(TestContext _) => Database.InitializeAsync();

    [ClassCleanup]
    public static Task ClassCleanup() => Database.DisposeAsync().AsTask();

    [TestMethod]
    public async Task Handle_ReturnsItemDetails_WithCurrentPrices()
    {
        // Arrange
        await using var db = await Database.CreateDbContextAsync();

        var userId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var utcNow = TimeProvider.System.GetUtcNow();

        var unitId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Email = $"{userId}@test.com", PasswordHash = "x" });
        db.Units.Add(new Unit { Id = unitId, Name = "Gram", Abbreviation = "g" });
        db.Ingredients.Add(new Ingredient
            { Id = ingredientId, UserId = userId, UnitId = unitId, Name = "Flour", UnitSize = 1000m });
        db.Recipes.Add(new Recipe
        {
            Id = recipeId, UserId = userId, Name = "Brownie Box", PortionCount = 12,
            VersionGroupId = Guid.NewGuid(), VersionNumber = 1, VersionLabel = "Standard",
            CostPerPortion = 2.50m, PackagingCost = 0.30m, TargetMargin = 0.35m
        });
        db.RecipeItems.Add(new RecipeItem
        {
            Id = Guid.NewGuid(), RecipeId = recipeId, IngredientId = ingredientId,
            Quantity = 500m, YieldPercentage = 1.0m
        });
        db.IngredientPriceHistories.Add(new IngredientPriceHistory
        {
            Id = Guid.NewGuid(), IngredientId = ingredientId, Price = 2.50m,
            UnitSize = 1000m, Source = PriceSource.Manual, CommittedAt = utcNow,
            EffectiveDate = DateOnly.FromDateTime(utcNow.UtcDateTime)
        });
        await db.SaveChangesAsync();

        var handler = new GetRecipeByIdHandler(db);

        // Act
        var result = await handler.Handle(new GetRecipeByIdQuery(recipeId, userId), CancellationToken.None);

        // Assert — Items list is populated and CurrentPrice matches price history
        Assert.IsFalse(result.IsError);
        Assert.AreEqual(1, result.Value.Items.Count);
        Assert.AreEqual(2.50m, result.Value.Items[0].CurrentPrice,
            "CurrentPrice should match the price from IngredientPriceHistory (C-3)");
    }

    [TestMethod]
    public async Task Handle_WrongUser_ReturnsNotFound()
    {
        // Arrange
        await using var db = await Database.CreateDbContextAsync();

        var ownerUserId = Guid.NewGuid();
        var attackerUserId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();

        db.Users.Add(new User { Id = ownerUserId, Email = $"{ownerUserId}@test.com", PasswordHash = "x" });
        db.Recipes.Add(new Recipe
        {
            Id = recipeId, UserId = ownerUserId, Name = "Secret Recipe", PortionCount = 4,
            VersionGroupId = Guid.NewGuid(), VersionNumber = 1
        });
        await db.SaveChangesAsync();

        var handler = new GetRecipeByIdHandler(db);

        // Act — attacker queries owner's recipe
        var result = await handler.Handle(new GetRecipeByIdQuery(recipeId, attackerUserId), CancellationToken.None);

        // Assert — returns NotFound, not the recipe
        Assert.IsTrue(result.IsError);
        Assert.AreEqual(ErrorType.NotFound, result.FirstError.Type);
    }
}