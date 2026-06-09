using NSubstitute;
using Nastart.Application.Common.Interfaces;
using Nastart.Application.Features.Recipes.Commands.UpdateRecipe;
using Nastart.Application.Tests.Infrastructure;
using Nastart.Domain.Entities;
using Nastart.Domain.Common;

namespace Nastart.Application.Tests.Features.Recipes;

[TestClass]
public sealed class UpdateRecipeHandlerTests
{
    private static readonly PostgreSqlTestDatabase Database = new();

    [ClassInitialize]
    public static Task ClassInitialize(TestContext _) => Database.InitializeAsync();

    [ClassCleanup]
    public static Task ClassCleanup() => Database.DisposeAsync().AsTask();

    [TestMethod]
    public async Task Handle_PortionCountChanged_TriggersCascadePerUniqueIngredient()
    {
        await using var db = await Database.CreateDbContextAsync();

        var userId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var ingredientId1 = Guid.NewGuid();
        var ingredientId2 = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Email = $"{userId}@test.com", PasswordHash = "x" });
        db.Units.Add(new Unit { Id = unitId, Name = "Gram", Abbreviation = "g" });

        db.Ingredients.Add(new Ingredient
        { Id = ingredientId1, UserId = userId, UnitId = unitId, Name = "Flour", UnitSize = 1000m });
        db.Ingredients.Add(new Ingredient
        { Id = ingredientId2, UserId = userId, UnitId = unitId, Name = "Sugar", UnitSize = 1000m });

        db.Recipes.Add(new Recipe
        {
            Id = recipeId, UserId = userId, Name = "Brownie Box", PortionCount = 12,
            VersionGroupId = Guid.NewGuid(), VersionNumber = 1, VersionLabel = "Standard"
        });
        db.RecipeItems.AddRange(
            new RecipeItem { Id = Guid.NewGuid(), RecipeId = recipeId, IngredientId = ingredientId1,
                Quantity = 500m, YieldPercentage = 1.0m },
            new RecipeItem { Id = Guid.NewGuid(), RecipeId = recipeId, IngredientId = ingredientId2,
                Quantity = 300m, YieldPercentage = 1.0m }
        );
        await db.SaveChangesAsync();

        var cascadeStub = Substitute.For<ICostCascadeService>();
        cascadeStub.RecalculateForIngredientAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new CascadeResult(1, 0));

        var handler = new UpdateRecipeHandler(db, cascadeStub);

        var result = await handler.Handle(
            new UpdateRecipeCommand(recipeId, userId, "Brownie Box", 24, 0.30m, 0.35m, "Standard"),
            CancellationToken.None);

        Assert.IsFalse(result.IsError);
        await cascadeStub.Received(2).RecalculateForIngredientAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Handle_PortionCountUnchanged_DoesNotTriggerCascade()
    {
        // Arrange
        await using var db = await Database.CreateDbContextAsync();

        var userId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Email = $"{userId}@test.com", PasswordHash = "x" });
        db.Recipes.Add(new Recipe
        {
            Id = recipeId, UserId = userId, Name = "Brownie Box", PortionCount = 12,
            VersionGroupId = Guid.NewGuid(), VersionNumber = 1, VersionLabel = "Standard"
        });
        await db.SaveChangesAsync();

        var cascadeStub = Substitute.For<ICostCascadeService>();
        cascadeStub.RecalculateForIngredientAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new CascadeResult(1, 0));

        var handler = new UpdateRecipeHandler(db, cascadeStub);

        // Act — change name only, same portion count
        var result = await handler.Handle(
            new UpdateRecipeCommand(recipeId, userId, "Brownie Box — Deluxe", 12, 0.30m, 0.35m, "Standard"),
            CancellationToken.None);

        // Assert — cascade NOT called (only name/packaging/margin changed, not portion count)
        Assert.IsFalse(result.IsError);
        await cascadeStub.DidNotReceive().RecalculateForIngredientAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}