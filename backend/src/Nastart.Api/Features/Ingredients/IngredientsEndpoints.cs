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

        var ingredient = await mediator.Send(command);
        return TypedResults.Created($"/ingredients/${ingredient.Id}", ingredient);
    }

    static async Task<IResult> Update(
        Guid id, 
        UpdateIngredientRequest request,
        IMediator mediator)
    {
        var command = new UpdateIngredientCommand(
            id,
            request.Name,
            request.Unit,
            request.CurrentPrice,
            request.CurrentStock,
            request.MinStock
        );

        var result = await mediator.Send(command);
        return result is not null ? TypedResults.Ok(result) : TypedResults.NotFound();
    }

    static async Task<IResult> Delete(Guid id, IMediator mediator)
    {
        var deleted = await mediator.Send(new DeleteIngredientCommand(id));
        
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}