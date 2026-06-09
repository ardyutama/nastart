using Nastart.Application.Features.Recipes.Queries.GetRecipes;
using Nastart.Application.Tests.Infrastructure;
using Nastart.Domain.Entities;

namespace Nastart.Application.Tests.Features.Recipes;

[TestClass]
public sealed class GetRecipesHandlerTests
{
    private static readonly PostgreSqlTestDatabase Database = new();

    [ClassInitialize]
    public static Task ClassInitialize(TestContext _) => Database.InitializeAsync();

    [ClassCleanup]
    public static Task ClassCleanup() => Database.DisposeAsync().AsTask();

    [TestMethod]
    public async Task Handle_ReturnsOnlyCurrentUserRecipes()
    {
        // Arrange
        await using var db = await Database.CreateDbContextAsync();

        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        db.Users.AddRange(
            new User
            {
                Id = userId,
                Email = $"user-{userId:N}@example.test",
                PasswordHash = "hashed-password",
                IsEmailVerified = true
            },
            new User
            {
                Id = otherUserId,
                Email = $"user-{otherUserId:N}@example.test",
                PasswordHash = "hashed-password",
                IsEmailVerified = true
            }
        );
        db.Recipes.AddRange(
            new Recipe { Id = Guid.NewGuid(), UserId = userId, Name = "My Brownie", PortionCount = 4, VersionGroupId = Guid.NewGuid() },
            new Recipe { Id = Guid.NewGuid(), UserId = otherUserId, Name = "Other Brownie", PortionCount = 4, VersionGroupId = Guid.NewGuid() }
        );
        await db.SaveChangesAsync();

        var handler = new GetRecipesHandler(db);

        var result = await handler.Handle(new GetRecipesQuery(userId), CancellationToken.None);

        Assert.IsFalse(result.IsError);
        Assert.HasCount(1, result.Value);
        Assert.AreEqual("My Brownie", result.Value[0].Name);
    }
}