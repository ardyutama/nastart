using ErrorOr;
using FluentValidation;
using MediatR;

namespace Nastart.Application.Common.Behaviors;

public class ValidationBehavior<TRequest, TResponse>(IValidator<TRequest>? validator = null)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : IErrorOr
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (validator is null)
            return await next(cancellationToken).ConfigureAwait(false);

        var validationResult = await validator
            .ValidateAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (validationResult.IsValid)
            return await next(cancellationToken).ConfigureAwait(false);

        var errors = validationResult.Errors
            .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
            .ToList();

        return (TResponse)(dynamic)errors;
    }
}