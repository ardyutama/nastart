using MediatR;
using Nastart.Application.Features.Ingredients.Queries.GetIngredients;

namespace Nastart.Api.Endpoints;

public static class IngredientEndpoints
{
    public static void MapIngredientEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/ingredients")
            .WithTags("Ingredients");

        group.MapGet("/", async (ISender sender, HttpContext httpContext, CancellationToken ct) =>
        {
            // var userId = httpContext.User.GetUserId();
            // var result = await sender.Send(new GetIngredientsQuery(userId), ct);
            return Results.Ok();
        });
    }
}