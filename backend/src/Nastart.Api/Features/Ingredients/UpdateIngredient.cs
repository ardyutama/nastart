using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Ingredients;

public record UpdateIngredientCommand(
    Guid Id,
    string? Name,
    string? Unit,
    decimal? CurrentPrice,
    decimal? CurrentStock,
    decimal? MinStock
) : IRequest<Ingredient>;

public class UpdateIngredientCommandValidator : AbstractValidator<UpdateIngredientCommand>
{
    public UpdateIngredientCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        When(x => x.Name is not null, () => 
            RuleFor(x => x.Name!).NotEmpty().MaximumLength(200)
        );

        When(x => x.Unit is not null, () => 
            RuleFor(x => x.Unit!).NotEmpty().MaximumLength(50)
        );

        When (x => x.CurrentPrice is not null, () => 
            RuleFor(x => x.CurrentPrice!).GreaterThan(0)
        );

        When (x => x.CurrentStock is not null, () => 
            RuleFor(x => x.CurrentStock!).GreaterThan(0)
        );

        When (x => x.MinStock is not null, () => 
            RuleFor(x => x.MinStock!).GreaterThan(0)
        );
    }
}

public class UpdateIngredientHandler(NastartDbContext db) : IRequestHandler<UpdateIngredientCommand, Ingredient?>
{
    public async Task<Ingredient?> Handle(
        UpdateIngredientCommand request,
        CancellationToken cancellationToken)
    {
        var ingredient = await db.Ingredients.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if(ingredient is null) return null;

        ingredient.Name = request.Name ?? ingredient.Name;
        ingredient.Unit = request.Unit ?? ingredient.Unit;
        ingredient.CurrentPrice = request.CurrentPrice ?? ingredient.CurrentPrice;
        ingredient.CurrentStock = request.CurrentStock ?? ingredient.CurrentStock;
        ingredient.MinStock = request.MinStock ?? ingredient.MinStock;

        await db.SaveChangesAsync(cancellationToken);
        return ingredient;
    }
}