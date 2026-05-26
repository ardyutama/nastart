using MediatR;
using Nastart.Api.Contracts.Recipe;
using Nastart.Api.Extensions;
using Nastart.Application.Features.Recipes.Commands.CreateRecipe;

namespace Nastart.Api.Endpoints;

public static class RecipeEndpoints
{
    public static void MapRecipeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("api/recipes")
            .WithTags("Recipes")
            .RequireAuthorization();
        
        group.MapPost("/", CreateRecipe)
            .WithName("CreateRecipe");

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
}