using FluentValidation;

namespace Nastart.Application.Features.Ingredients.Commands.CreateIngredient;

public class CreateIngredientCommandValidator : AbstractValidator<CreateIngredientCommand>
{
    public CreateIngredientCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Ingredient name is required.")
            .MaximumLength(255).WithMessage("Name cannot exceed 255 characters.");

        RuleFor(x => x.UnitId)
            .NotEmpty().WithMessage("UnitId is required.");

        RuleFor(x => x.UnitSize)
            .GreaterThan(0).WithMessage("Unit size must be greater than zero.");

        RuleFor(x => x.PriceSpikeThresholdPct)
            .InclusiveBetween(1, 100)
            .When(x => x.PriceSpikeThresholdPct.HasValue)
            .WithMessage("Spike threshold must be between 1% and 100%");

        RuleFor(x => x.InitialPrice)
            .GreaterThan(0).When(x => x.InitialPrice.HasValue)
            .WithMessage("Initial price must be greater than zero.");

        RuleFor(x => x.EffectiveDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.EffectiveDate.HasValue)
            .WithMessage("Effective date cannot be in the future");
    }
}