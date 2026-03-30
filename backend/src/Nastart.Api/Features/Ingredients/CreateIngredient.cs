using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Ingredients;

public record CreateIngredientCommand(
    string Name,
    string Unit,
    decimal CurrentPrice,
    decimal CurrentStock,
    decimal MinStock
) : IRequest<Ingredient>;

public class CreateIngredientCommandValidator : AbstractValidator<CreateIngredientCommand>
{
    public CreateIngredientCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CurrentPrice).GreaterThan(0);
        RuleFor(x => x.Unit).MaximumLength(50).When(x => x.Unit is not null);
    }
}
public class CreateIngredientHandler(NastartDbContext db) : IRequestHandler<CreateIngredientCommand, Ingredient>
{
    public async Task<Ingredient> Handle(
        CreateIngredientCommand request,
        CancellationToken cancellationToken)
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
        await db.SaveChangesAsync(cancellationToken);

        return ingredient;
    }
}