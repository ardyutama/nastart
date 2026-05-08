using ErrorOr;

namespace Nastart.Api.Extensions;

public static class ResultExtensions
{
    public static IResult ToApiResult<T>(this ErrorOr<T> result)
    {
        if(!result.IsError)
            return Results.Ok(result.Value);

        return result.FirstError.Type switch
        {
            ErrorType.Validation => Results.BadRequest(new
            {
                errors = result.Errors.Select(e => new {e.Code, e.Description})
            }),
            ErrorType.NotFound => Results.NotFound(new
            {
                error = result.FirstError.Description
            }),
            ErrorType.Conflict => Results.Conflict(new
            {
                error = result.FirstError.Description
            }),
            ErrorType.Unauthorized => Results.Unauthorized(),
            ErrorType.Forbidden => Results.Forbid(),
            _ => Results.Problem(detail: result.FirstError.Description, statusCode: 500)
        };
    }

    public static IResult ToCreatedResult<T>(this ErrorOr<T> result, string location)
    {
        if(!result.IsError)
            return Results.Created(location, result.Value);
        
        return result.ToApiResult();
    }
}