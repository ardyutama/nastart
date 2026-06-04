using MediatR;
using Nastart.Api.Contracts.Recipe;
using Nastart.Api.Extensions;
using Nastart.Application.Features.Recipes.Commands.AddRecipeItem;
using Nastart.Application.Features.Recipes.Commands.CreateRecipe;
using Nastart.Application.Features.Recipes.Commands.RemoveRecipeItem;
using Nastart.Application.Features.Recipes.Queries.GetRecipeById;
using Nastart.Application.Features.Recipes.Queries.GetRecipes;
using Nastart.Domain.Entities;

namespace Nastart.Api.Endpoints;

public static class RecipeEndpoints
{
    public static void MapRecipeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/recipes")
            .WithTags("Recipes")
            .RequireAuthorization();

        group.MapGet("/", GetRecipes)
            .WithName("GetRecipes");

        group.MapPost("/", CreateRecipe)
            .WithName("CreateRecipe");

        group.MapGet("/{recipeId:guid}", GetRecipeById)
            .WithName("GetRecipeById");

        var itemsGroup = group.MapGroup("/{recipeId:guid}/items")
            .WithTags("Recipe Items");

        itemsGroup.MapPost("/", AddRecipeItem)
            .WithName("AddRecipeItem");

        itemsGroup.MapDelete("/{recipeItemId:guid}", RemoveRecipeItem)
            .WithName("RemoveRecipeItem");

    }

    private static async Task<IResult> GetRecipes(
        HttpContext httpContext, ISender sender, CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();

        var query = new GetRecipesQuery(userId);
        var result = await sender.Send(query, ct);

        return result.ToApiResult();
    }

    private static async Task<IResult> CreateRecipe(
           CreateRecipeRequest request, HttpContext httpContext, ISender sender, CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();
        var command = new CreateRecipeCommand(
            userId,
            request.Name,
            request.PortionCount,
             request.RecipeItems.Select(r => new CreateRecipeItemDto(
                r.IngredientId, r.Quantity, r.YieldPercentage
            )).ToArray(),
            request.PackagingCost,
            request.TargetMargin,
            request.VesionLabel
        );

        var result = await sender.Send(command, ct);
        return result.ToCreatedResult($"/api/recipes/{result.Value?.Id}");
    }

    private static async Task<IResult> GetRecipeById(
        Guid recipeId, HttpContext httpContext, ISender sender, CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();
        var query = new GetRecipeByIdQuery(recipeId, userId);
        var result = await sender.Send(query, ct);

        return result.ToApiResult();
    }

    private static async Task<IResult> AddRecipeItem(
        Guid recipeId, AddRecipeItemRequest request, HttpContext httpContext, ISender sender, CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();
        var command = new AddRecipeItemCommand(
            recipeId,
            userId,
            request.IngredientId,
            request.Quantity,
            request.YieldPercentage
        );

        var result = await sender.Send(command, ct);
        return result.ToCreatedResult($"/api/recipes/{recipeId}/items/{result.Value?.RecipeItemId}");
    }

    private static async Task<IResult> RemoveRecipeItem(
        Guid recipeId, Guid recipeItemId, HttpContext httpContext, ISender sender, CancellationToken ct)
    {
        var userId = httpContext.User.GetUserId();
        var command = new RemoveRecipeItemCommand(recipeItemId, recipeId, userId);
        var result = await sender.Send(command, ct);

        return result.ToApiResult();
    }
}