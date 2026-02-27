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
        var ingredients = IngredientStore.GetAll();

        return TypedResults.Ok(ingredients);
    }

    static IResult GetById(Guid id)
    {
        var ingredient = IngredientStore.GetById(id);
        return ingredient is not null
            ? TypedResults.Ok(ingredient)
            : TypedResults.NotFound();
    }

    static IResult Create(CreateIngredientRequest request)
    {
        var ingredient = new Ingredient
        {
            Name = request.Name,
            Unit = request.Unit,
            CurrentPrice = request.CurrentPrice,
            CurrentStock = request.CurrentStock,
            MinStock = request.MinStock
        };

        var created = IngredientStore.Add(ingredient);

        return TypedResults.Created($"/ingredients/${created.Id}", created);
    }

    static IResult Update(Guid id, UpdateIngredientRequest request)
    {
        var existing = IngredientStore.GetById(id);
        if (existing is null) return TypedResults.NotFound();

        existing.Name = request.Name ?? existing.Name;
        existing.Unit = request.Unit ?? existing.Unit;
        existing.CurrentPrice = request.CurrentPrice ?? existing.CurrentPrice;
        existing.CurrentStock = request.CurrentStock ?? existing.CurrentStock;
        existing.MinStock = request.MinStock ?? existing.MinStock;

        IngredientStore.Update(id, existing);
        return TypedResults.Ok(existing);
    }

    static IResult Delete(Guid id)
    {
        var deleted = IngredientStore.Delete(id);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}