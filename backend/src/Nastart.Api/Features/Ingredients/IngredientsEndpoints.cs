using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Nastart.Api.Shared.Validation;

namespace Nastart.Api.Features.Ingredients;

public static class IngredientsEndpoints
{
    public static WebApplication MapIngredientsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/ingredients")
            .WithTags("Ingredients");

        group.MapGet("/", GetAll)
            .WithSummary("List all ingredients")
            .WithDescription("Return the full ingredient catalogue with current prices.");

        group.MapGet("/{id:guid}", GetById)
            .WithSummary("Get ingredient by ID")
            .Produces<Ingredient>(200)
            .Produces(404);

        group.MapPost("/", Create)
            .WithSummary("Create a new ingredient")
            .Produces<Ingredient>(201)
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", Update)
            .WithSummary("Update an ingredient")
            .Produces<Ingredient>(200)
            .Produces(404)
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}", Delete)
            .WithSummary("Delete an ingredient")
            .Produces(204)
            .Produces(404);

        return app;
    }

    static async Task<IResult> GetAll(IMediator mediator)
    {
        var ingredients = await mediator.Send(new GetIngredientsQuery());

        return TypedResults.Ok(ingredients);
    }

    static async Task<IResult> GetById(Guid id, IMediator mediator)
    {
        var ingredient = await mediator.Send(new GetIngredientsByIdQuery(id));
        return ingredient is not null
            ? TypedResults.Ok(ingredient)
            : TypedResults.NotFound();
    }

    static async Task<IResult> Create(CreateIngredientRequest request, IMediator mediator)
    {
        var command = new CreateIngredientCommand(
            request.Name,
            request.Unit,
            request.CurrentPrice,
            request.CurrentStock,
            request.MinStock
        );

        var ingredient = mediator.Send(command);
        return TypedResults.Created($"/ingredients/${ingredient.Id}", ingredient);
    }

    static async Task<IResult> Update(
        Guid id, 
        UpdateIngredientRequest request, 
        IValidator<UpdateIngredientRequest> validator,
        NastartDbContext db)
    {
        var validationError = await ValidationHelper.ValidateAsync(validator, request);
        if(validationError is not null) return validationError;

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