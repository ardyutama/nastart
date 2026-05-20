using MediatR;
using Nastart.Api.Extensions;
using Nastart.Application.Features.Ingredients.Commands.AddIngredientPrice;
using Nastart.Application.Features.Ingredients.Commands.CreateIngredient;
using Nastart.Application.Features.Ingredients.Commands.DeleteIngredient;
using Nastart.Application.Features.Ingredients.Commands.UpdateIngredient;
using Nastart.Application.Features.Ingredients.Queries.GetIngredientById;
using Nastart.Application.Features.Ingredients.Queries.GetIngredientPriceHistory;
using Nastart.Application.Features.Ingredients.Queries.GetIngredients;

namespace Nastart.Api.Endpoints;

public static class IngredientEndpoints
{
    public static void MapIngredientEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/ingredients")
            .WithTags("Ingredients")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var result = await sender.Send(new GetIngredientsQuery(userId), ct);
            return Results.Ok(result);
        }).WithName("GetIngredients"); ;

        group.MapGet("/{ingredientId:guid}", async (
            Guid ingredientId, HttpContext httpContext, ISender sender, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var result = await sender.Send(new GetIngredientByIdQuery(ingredientId, UserId: userId), ct);
            return result.ToApiResult();
        }).WithName("GetIngredientById");

        group.MapPost("/", async (CreateIngredientCommand command, ISender sender, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var cmd = command with { UserId = userId };

            var result = await sender.Send(cmd, ct);
            return result.ToCreatedResult($"/api/ingredients/{result.Value!.Id}");
        }).WithName("CreateIngredient");

        group.MapPut("/{ingredientId:guid}", async (
            Guid ingredientId, UpdateIngredientCommand command, HttpContext httpContext,
            ISender sender, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var cmd = command with { UserId = userId, IngredientId = ingredientId };

            var result = await sender.Send(cmd, ct);
            return result.ToApiResult();
        }).WithName("UpdateIngredient");

        group.MapDelete("/{ingredientId:guid}", async (
            Guid ingredientId, HttpContext httpContext,
            ISender sender, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var result = await sender.Send(new DeleteIngredientCommand(ingredientId, UserId: userId), ct);
            return result.ToApiResult();
        }).WithName("DeleteIngredient");

        var pricesGroup = group.MapGroup("/{ingredientId:guid}/prices")
            .WithTags("Ingredient Prices");

        pricesGroup.MapPost("/", async (
            Guid ingredientId, AddIngredientPriceCommand command,
            HttpContext httpContext, ISender sender, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var cmd = command with { UserId = userId, IngredientId = ingredientId };
            var result = await sender.Send(cmd, ct);
            return result.ToCreatedResult($"/api/ingredients/{ingredientId}/prices");
        }).WithName("AddIngredientPrice");

        pricesGroup.MapGet("/", async (
            Guid ingredientId, HttpContext httpContext, ISender sender, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var result = await sender.Send(new GetIngredientPriceHistoryQuery(ingredientId, UserId: userId), ct);
            return result.ToApiResult();
        }).WithName("GetIngredientPriceHistory");
    }
}