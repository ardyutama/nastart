using NSubstitute;
using Nastart.Application.Common.Interfaces;
using Nastart.Application.Features.Recipes.Commands.CreateRecipeVersion;
using Nastart.Application.Tests.Infrastructure;
using Nastart.Domain.Entities;
using Nastart.Domain.Common;

namespace Nastart.Application.Tests.Features.Recipes;

[TestClass]
public sealed class CreateRecipeVersionHandlerTests
{
    private static readonly PostgreSqlTestDatabase Database = new();

    [ClassInitialize]
    public static Task ClassInitialize(TestContext _) => Database.InitializeAsync();

    [ClassCleanup]
    public static Task ClassCleanup() => Database.DisposeAsync().AsTask();

    private static ICostCascadeService MakeCascadeStub()
    {
        var stub = Substitute.For<ICostCascadeService>();
        stub.RecalculateForIngredientAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new CascadeResult(1, 0));
        return stub;
    }

    [TestMethod]
    public async Task Handle_NewVersion_SharesSameVersionGroupId()
    {
        // Arrange
        await using var db = await Database.CreateDbContextAsync();

        var userId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var versionGroupId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();

        var unitId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Email = $"{userId}@test.com", PasswordHash = "x" });
        db.Units.Add(new Unit { Id = unitId, Name = "Gram", Abbreviation = "g" });

        db.Ingredients.Add(new Ingredient
        { Id = ingredientId, UserId = userId, UnitId = unitId, Name = "Flour", UnitSize = 1000m });

        db.Recipes.Add(new Recipe
        {
            Id = sourceId,
            UserId = userId,
            Name = "Brownie",
            PortionCount = 12,
            VersionGroupId = versionGroupId,
            VersionNumber = 1,
            VersionLabel = "Standard"
        });
        db.RecipeItems.Add(new RecipeItem
        {
            Id = Guid.NewGuid(),
            RecipeId = sourceId,
            IngredientId = ingredientId,
            Quantity = 500m,
            YieldPercentage = 1.0m
        });
        await db.SaveChangesAsync();

        var handler = new CreateRecipeVersionHandler(db, MakeCascadeStub());

        // Act
        var result = await handler.Handle(
            new CreateRecipeVersionCommand(sourceId, userId, "Premium"), CancellationToken.None);

        // Assert — new version shares the same VersionGroupId as source
        Assert.IsFalse(result.IsError);
        Assert.AreEqual(versionGroupId, result.Value.VersionGroupId,
            "New recipe version must share VersionGroupId with source (groups all versions of same concept)");
    }

    [TestMethod]
    public async Task Handle_NewVersion_HasIncrementedVersionNumber()
    {
        // Arrange
        await using var db = await Database.CreateDbContextAsync();

        var userId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Email = $"{userId}@test.com", PasswordHash = "x" });
        db.Units.Add(new Unit { Id = unitId, Name = "Gram", Abbreviation = "g" });

        db.Ingredients.Add(new Ingredient
        { Id = ingredientId, UserId = userId, UnitId = unitId, Name = "Flour", UnitSize = 1000m });
        db.Recipes.Add(new Recipe
        {
            Id = sourceId,
            UserId = userId,
            Name = "Brownie",
            PortionCount = 12,
            VersionGroupId = Guid.NewGuid(),
            VersionNumber = 1,
            VersionLabel = "Standard"
        });
        db.RecipeItems.Add(new RecipeItem
        {
            Id = Guid.NewGuid(),
            RecipeId = sourceId,
            IngredientId = ingredientId,
            Quantity = 500m,
            YieldPercentage = 1.0m
        });
        await db.SaveChangesAsync();

        var handler = new CreateRecipeVersionHandler(db, MakeCascadeStub());

        // Act
        var result = await handler.Handle(
            new CreateRecipeVersionCommand(sourceId, userId, "Premium"), CancellationToken.None);

        // Assert — version number increments from 1 → 2
        Assert.IsFalse(result.IsError);
        Assert.AreEqual(2, result.Value.VersionNumber,
            "VersionNumber should be source.VersionNumber + 1");
    }
}