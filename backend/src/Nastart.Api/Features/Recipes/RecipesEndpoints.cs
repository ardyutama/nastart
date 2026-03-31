using MediatR;
using Nastart.Api.Features.Recipes;

public static class RecipesEndpoints
{
    public static WebApplication MapRecipesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/recipes")
            .WithTags("Recipes");

        group.MapGet("/", GetRecipes)
            .WithSummary("List all Recipes")
            .WithDescription("Return the full recipes");

        return app;
    }

    static async Task<IResult> GetRecipes(IMediator mediator, Guid UserId)
    {
        var recipes = await mediator.Send(new GetRecipesQuery(UserId));

        return TypedResults.Ok(recipes);
    }
}