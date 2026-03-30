using FluentValidation;

namespace Nastart.Api.Shared.Validation;

public static class ValidationHelper
{
    public static async Task<IResult?> ValidateAsync<T>(
        IValidator<T> validator,
        T request
    )
    {
        var result = await validator.ValidateAsync(request);

        if (result.IsValid) return null;

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );
        return TypedResults.ValidationProblem(errors);
    }
}