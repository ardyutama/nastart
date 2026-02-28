using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

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

    static async Task<IResult> GetAll(NastartDbContext db)
    {
        var ingredients = await db.Ingredients.ToListAsync();

        return TypedResults.Ok(ingredients);
    }

    static async Task<IResult> GetById(Guid id, NastartDbContext db)
    {
        var ingredient = await db.Ingredients.FindAsync(id);
        return ingredient is not null
            ? TypedResults.Ok(ingredient)
            : TypedResults.NotFound();
    }

    static async Task<IResult> Create(CreateIngredientRequest request, NastartDbContext db)
    {
        var ingredient = new Ingredient
        {
            Name = request.Name,
            Unit = request.Unit,
            CurrentPrice = request.CurrentPrice,
            CurrentStock = request.CurrentStock,
            MinStock = request.MinStock
        };

        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/ingredients/${ingredient.Id}", ingredient);
    }

    static async Task<IResult> Update(Guid id, UpdateIngredientRequest request, NastartDbContext db)
    {
        var existing = await db.Ingredients.FindAsync(id);
        if (existing is null) return TypedResults.NotFound();

        existing.Name = request.Name ?? existing.Name;
        existing.Unit = request.Unit ?? existing.Unit;
        existing.CurrentPrice = request.CurrentPrice ?? existing.CurrentPrice;
        existing.CurrentStock = request.CurrentStock ?? existing.CurrentStock;
        existing.MinStock = request.MinStock ?? existing.MinStock;

        await db.SaveChangesAsync();
        return TypedResults.Ok(existing);
    }

    static async Task<IResult> Delete(Guid id, NastartDbContext db)
    {
        var ingredient = await db.Ingredients.FindAsync(id);
        if (ingredient is null) return TypedResults.NotFound();

        db.Ingredients.Remove(ingredient);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }
}