using FluentValidation;

namespace Nastart.Application.Features.Recipes.Commands.CreateRecipe;

public class CreateRecipeCommandValidator : AbstractValidator<CreateRecipeCommand>
{
    public CreateRecipeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Recipe name is required.")
            .MaximumLength(200).WithMessage("Recipe name cannot exceed 200 characters.");
        
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");
        
        RuleFor(x => x.PortionCount)
            .GreaterThanOrEqualTo(1).WithMessage("Portion count must be at least 1.");
        
        RuleFor(x => x.PackagingCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TargetMargin).InclusiveBetween(0m, 0.99m).WithMessage("Target margin must be between 0% and 99%");

        RuleFor(x => x.RecipeItems)
            .NotEmpty().WithMessage("Recipe must have at least one item.")
            .Must(items => items.All(i => i.Quantity > 0))
                .WithMessage("Each recipe item quantity must be greater than zero.")
            .Must(items => items.All(i => i.YieldQuantity > 0 && i.YieldQuantity <= 1.0m))
                .WithMessage("Each recipe item yield percentage must be between 0 and 100%.");
    }
}