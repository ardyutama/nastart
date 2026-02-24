namespace Nastart.Api.Features.Ingredients;

public static class IngredientsEndpoints
{
    public static WebApplication MapIngredientsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/ingredients")
            .WithTags("Ingredients");
        
        group.MapGet("/", GetAll);
        group.MapGet("/{id:guid}", GetById);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);

        return app;
    }

    static IResult GetAll()
    {
        var ingredients = new List<Ingredient>
        {
            new() {Id = Guid.NewGuid(), Name = "Flour", Unit = "Kg", CurrentPrice = 12_000},
            new() {Id = Guid.NewGuid(), Name = "Flour", Unit = "Kg", CurrentPrice = 12_000},
            new() {Id = Guid.NewGuid(), Name = "Flour", Unit = "Kg", CurrentPrice = 12_000},
        };
    }
}