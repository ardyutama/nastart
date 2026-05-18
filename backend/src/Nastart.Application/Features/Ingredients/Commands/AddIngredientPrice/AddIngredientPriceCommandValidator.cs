using FluentValidation;

namespace Nastart.Application.Features.Ingredients.Commands.AddIngredientPrice;

public class AddIngredientPriceCommandValidator : AbstractValidator<AddIngredientPriceCommand>
{
    public AddIngredientPriceCommandValidator()
    {
        RuleFor(x => x.IngredientId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero.");

        RuleFor(x => x.EffectiveDate)
            .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.EffectiveDate.HasValue)
            .WithMessage("Effective date cannot be in the future.");
    }
}