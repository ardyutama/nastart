using FluentValidation;

namespace Nastart.Api.Features.Ingredients;

public class CreateIngredientValidator : AbstractValidator<CreateIngredientRequest>
{
    public CreateIngredientValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Name must be between 1 and 200 characters.");

        RuleFor(x => x.CurrentPrice)
            .GreaterThan(0)
            .WithMessage("Price must be greater than zero.");

        RuleFor(x => x.Unit)
            .MaximumLength(50)
            .When(x => x.Unit is not null);
    }
}

public class UpdateIngredientValidator : AbstractValidator<UpdateIngredientRequest>
{
    public UpdateIngredientValidator()
    {
        When(x => x.Name is not null, () =>
        {
            RuleFor(x => x.Name!).NotEmpty().MaximumLength(200);
        });

        When(x => x.CurrentPrice is not null, () =>
        {
            RuleFor(x => x.CurrentPrice!.Value).GreaterThan(0);
        });

        When(x => x.Unit is not null, () =>
        {
            RuleFor(x => x.Unit!).MaximumLength(50);
        });
    }
}