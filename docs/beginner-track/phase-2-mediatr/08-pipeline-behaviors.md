# Lesson 08: Pipeline Behaviors

## 🎯 What you'll build
Add two MediatR pipeline behaviors that run automatically around every handler: `ValidationBehavior` (validates commands before the handler runs) and `LoggingBehavior` (logs every request/response). No more manual validation calls in each handler.

---

## 🔧 The .NET Way

### What is an IPipelineBehavior?

A pipeline behavior wraps every `mediator.Send()` call. It's middleware for MediatR — like middleware in Express.js, FastAPI, or ASP.NET Core itself:

```
mediator.Send(command)
    │
    ▼
LoggingBehavior.Handle()    ← logs "Handling CreateIngredientCommand"
    │
    ▼
ValidationBehavior.Handle() ← runs FluentValidation, throws if invalid
    │
    ▼
CreateIngredientHandler.Handle() ← your actual business logic
    │
    ▼
ValidationBehavior.Handle() ← (after handler) resumes
    │
    ▼
LoggingBehavior.Handle()    ← logs "Handled in 12ms"
    │
    ▼
result returned to caller
```

Compare:
```python
# FastAPI middleware
@app.middleware("http")
async def log_requests(request, call_next):
    start = time.time()
    response = await call_next(request)
    print(f"Took {time.time() - start:.2f}s")
    return response
```

### Why use behaviors?

Without behaviors, every handler has to manually call the validator:
```csharp
// Every handler looks like this without behaviors:
var validationError = await ValidationHelper.ValidateAsync(validator, request);
if (validationError is not null) throw new ValidationException(...);
```

With `ValidationBehavior`, you never write that again. It happens automatically.

---

## Step 1 — Create the ValidationBehavior

Create `src/Nastart.Api/Shared/Behaviors/ValidationBehavior.cs`:

```csharp
using FluentValidation;
using MediatR;

namespace Nastart.Api.Shared.Behaviors;

/// <summary>
/// MediatR pipeline behavior that runs FluentValidation validators
/// before every handler. If validation fails, throws ValidationException.
/// Handlers never need to call validators manually.
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
// ↑ Generic constraint: TRequest must be an IRequest<TResponse>
// This behavior applies to ALL request types automatically
{
    // IEnumerable<IValidator<TRequest>> = all validators registered for this request type
    // If there are none, this is just an empty collection — no errors.

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,    // ← the next behavior or handler in the pipeline
        CancellationToken cancellationToken)
    {
        // Skip if no validators registered for this request type
        if (!validators.Any())
            return await next();
        //              ↑ next() = call the next step in the pipeline

        // Run all validators in parallel
        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken))
        );
        // WhenAll = like Python's asyncio.gather() or Promise.all()

        // Collect all failures
        var failures = results
            .SelectMany(r => r.Errors)
            .Where(e => e is not null)
            .ToList();

        // If any failures, throw — the endpoint catches this in the exception handler
        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

---

## Step 2 — Create the LoggingBehavior

Create `src/Nastart.Api/Shared/Behaviors/LoggingBehavior.cs`:

```csharp
using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Nastart.Api.Shared.Behaviors;

/// <summary>
/// Logs every MediatR request with its execution time.
/// Runs around every handler automatically.
/// </summary>
public class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;   // e.g., "CreateIngredientCommand"
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Handling {RequestName}", requestName);
        // ↑ Structured logging — {RequestName} is a named hole, not string interpolation.
        //   This lets log aggregators (like Seq, Splunk) index the field.
        //   Compare: Python logging.info("Handling %s", request_name)

        try
        {
            var response = await next();

            stopwatch.Stop();
            logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(
                ex,
                "Error handling {RequestName} after {ElapsedMs}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);

            throw;  // re-throw — don't swallow exceptions
        }
    }
}
```

---

## Step 3 — Register the behaviors in Program.cs

Behaviors run in the order they're registered. Logging first (outer), then validation (inner), then the handler.

```csharp
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();

    // Register behaviors — order matters: first registered = outermost wrapper
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});
```

---

## Step 4 — Handle ValidationException at the API level

When `ValidationBehavior` throws a `ValidationException`, the endpoint needs to catch it and return a `400` response. Add global exception handling in `Program.cs`:

```csharp
using FluentValidation;

// Add this AFTER app = builder.Build() and BEFORE app.MapGet etc.

// UseExceptionHandler catches unhandled exceptions and returns ProblemDetails
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exceptionHandlerFeature = context.Features
            .Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

        if (exceptionHandlerFeature?.Error is ValidationException validationEx)
        {
            // Map FluentValidation errors → RFC 7807 format
            var errors = validationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new
            {
                type   = "https://tools.ietf.org/html/rfc7807",
                title  = "Validation failed",
                status = 400,
                errors
            });

            return;
        }

        // Fallback for all other exceptions — 500
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type   = "https://tools.ietf.org/html/rfc7807",
            title  = "An unexpected error occurred",
            status = 500
        });
    });
});
```

---

## Step 5 — Remove manual validation from handlers

Now that `ValidationBehavior` does it automatically, remove the manual validation from `CreateIngredient.cs`:

```csharp
// In CreateIngredientHandler.Handle() — remove the manual validation:
// The validator is now registered and runs automatically via ValidationBehavior.
// The handler only needs to focus on the business logic.

public class CreateIngredientHandler(NastartDbContext db)
    : IRequestHandler<CreateIngredientCommand, Ingredient>
{
    public async Task<Ingredient> Handle(
        CreateIngredientCommand request,
        CancellationToken cancellationToken)
    {
        // No manual validation needed here anymore ← behaviors handle it
        var ingredient = new Ingredient
        {
            Name = request.Name,
            Unit = request.Unit,
            CurrentPricePerUnit = request.CurrentPricePerUnit,
        };

        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync(cancellationToken);

        return ingredient;
    }
}
```

---

## ✅ Run it and verify

```powershell
dotnet run
```

**Validation still works:**
```powershell
curl -X POST https://localhost:7xxx/ingredients `
  -H "Content-Type: application/json" `
  -d '{"name": "", "currentPricePerUnit": 0}'
# Returns: 400 ProblemDetails — validation kicked in via behavior
```

**Check the terminal logs** — you'll see structured log output for every request:
```
info: Handling CreateIngredientCommand
info: Handled CreateIngredientCommand in 8ms
```

---

## 🔑 Key Concepts

| Concept | What it is |
|---------|-----------|
| `IPipelineBehavior<TReq,TRes>` | Middleware for MediatR — wraps every handler call |
| `RequestHandlerDelegate<TRes>` | A delegate to the next step in the pipeline — call it with `await next()` |
| Pipeline order | First registered = outermost = runs first before and last after |
| Structured logging | `{PropertyName}` holes in log messages — machine-searchable, not just strings |
| `ILogger<T>` | .NET's built-in logging interface — injected from DI |
| `ValidationException` | FluentValidation's exception — thrown when validation fails |
| Global exception handler | Catches unhandled exceptions → consistent ProblemDetails response |
| `Task.WhenAll()` | Run multiple async operations in parallel — like `asyncio.gather()` / `Promise.all()` |

---

➡️ Next: [Lesson 09 — Refactor all features](09-refactor-all-features.md)
