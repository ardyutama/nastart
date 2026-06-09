using FluentValidation;

namespace Nastart.Application.Features.Recipes.Commands.AddRecipeItem;

public class AddRecipeItemCommandValidator : AbstractValidator<AddRecipeItemCommand>
{
    public AddRecipeItemCommandValidator()
    {
        RuleFor(x => x.RecipeId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.IngredientId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
        RuleFor(x => x.YieldPercentage)
            .GreaterThan(0).LessThanOrEqualTo(1.0m)
            .WithMessage("Yield percentage must be between 0 and 100%.");
    }
}