using FluentValidation;

namespace Nastart.Application.Features.Recipes.Commands.UpdateRecipe;

public class UpdateRecipeCommandValidator : AbstractValidator<UpdateRecipeCommand>
{
    public UpdateRecipeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Recipe name is required.")
            .MaximumLength(200).WithMessage("Recipe name cannot exceed 200 characters.");

        RuleFor(x => x.PortionCount)
            .GreaterThanOrEqualTo(1).WithMessage("Portion count must be at least 1.");

        RuleFor(x => x.PackagingCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TargetMargin).InclusiveBetween(0m, 0.99m).WithMessage("Target margin must be between 0% and 99% for user input.");    }
}