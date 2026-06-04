using ErrorOr;
using NSubstitute;
using Nastart.Application.Common.Interfaces;
using Nastart.Application.Features.Recipes.Commands.AddRecipeItem;
using Nastart.Application.Tests.Infrastructure;
using Nastart.Domain.Entities;
using Nastart.Domain.Common;

namespace Nastart.Application.Tests.Features.Recipes;

[TestClass]
public sealed class AddRecipeItemHandlerTests
{
    private static readonly PostgreSqlTestDatabase Database = new();

    [ClassInitialize]
    public static Task ClassInitialize(TestContext _) => Database.InitializeAsync();

    [ClassCleanup]
    public static Task ClassCleanup() => Database.DisposeAsync().AsTask();

    [TestMethod]
    public async Task Handle_DuplicateIngredient_ReturnsConflict()
    {
        await using var db = await Database.CreateDbContextAsync();

        var userId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        db.Users.Add(new User { Id = userId, Email = $"{userId}@test.com", PasswordHash = "x" });
        db.Units.Add(new Unit { Id = unitId, Name = "Gram", Abbreviation = "g" });
        db.Ingredients.Add(new Ingredient
            { Id = ingredientId, UserId = userId, UnitId = unitId, Name = "Flour", UnitSize = 1000m });
        db.Recipes.Add(new Recipe
        {
            Id = recipeId, UserId = userId, Name = "Brownie Box", PortionCount = 12,
            VersionGroupId = Guid.NewGuid(), VersionNumber = 1
        });
        db.RecipeItems.Add(new RecipeItem
        {
            Id = Guid.NewGuid(), RecipeId = recipeId, IngredientId = ingredientId,
            Quantity = 500m, YieldPercentage = 1.0m
        });
        await db.SaveChangesAsync();

        var cascadeStub = Substitute.For<ICostCascadeService>();
        cascadeStub.RecalculateForIngredientAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new CascadeResult(1, 0));

        var handler = new AddRecipeItemHandler(db, cascadeStub);

        var result = await handler.Handle(
            new AddRecipeItemCommand(recipeId, userId, ingredientId, 300m, 0.95m),
            CancellationToken.None);

        Assert.IsTrue(result.IsError);
        Assert.AreEqual(ErrorType.Conflict, result.FirstError.Type,
            "Adding the same ingredient twice should return Conflict, not a second item");
    }
}