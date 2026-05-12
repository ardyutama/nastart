using MediatR;
using Nastart.Api.Extensions;
using Nastart.Application.Features.Ingredients.Commands.CreateIngredient;
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
        });

        group.MapPost("/", async (CreateIngredientRequest request, ISender sender, HttpContext httpContext, CancellationToken ct) =>
        {
            var userId = httpContext.User.GetUserId();
            var command = new CreateIngredientCommand(
                Name: request.Name,
                UserId: userId,
                CategoryId: request.CategoryId,
                UnitId: request.UnitId,
                UnitSize: request.UnitSize,
                PriceSpikeThresholdPct: request.PriceSpikeThresholdPct,
                InitialPrice: request.InitialPrice,
                EffectiveDate: request.EffectiveDate
            );

            var result = await sender.Send(command, ct);

            if (result.IsError)
                return result.ToApiResult();

            return Results.Created($"/api/ingredients/{result.Value!.Id}", result.Value);
        });
    }

    public record CreateIngredientRequest(
        string Name,
        Guid? CategoryId,
        Guid UnitId,
        decimal UnitSize,
        decimal PriceSpikeThresholdPct = 10m,
        decimal? InitialPrice = null,
        DateOnly? EffectiveDate = null
    );
}