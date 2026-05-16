using FluentValidation;

namespace Nastart.Application.Features.Ingredients.Commands.UpdateIngredient;

public class UpdateIngredientCommandValidator : AbstractValidator<UpdateIngredientCommand>
{
    public UpdateIngredientCommandValidator()
    {
        RuleFor(x => x.IngredientId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ingredient Name is required")
            .MaximumLength(255).WithMessage("Name cannot exceed 255 characters");

        RuleFor(x => x.UnitId).NotEmpty().WithMessage("UnitId is required.");

        RuleFor(x => x.UnitSize)
            .GreaterThan(0).WithMessage("Unit size must be greater than zero");

        RuleFor(x => x.PriceSpikeThresholdPct)
            .InclusiveBetween(0, 100)
            .When(x => x.PriceSpikeThresholdPct.HasValue)
            .WithMessage("Spike threshold must be between 0 and 100.");
    }
}