# Lesson 19: Registration — Creating a User Account

> **Phase**: 5 — Building the Full Application
> **Prerequisite**: Lesson 18. `users` table exists in the database.
> **What you build**: `POST /auth/register` — validates email uniqueness, hashes the password, saves the user.

---

## What You Will Learn

- Why you never store plaintext passwords
- BCrypt password hashing with `BCrypt.Net-Next`
- Returning `409 Conflict` when the email already exists
- The `RegisterCommand` → `RegisterHandler` → endpoint pattern

---

## Why Password Hashing?

```python
# ❌ What NOT to do (Python equivalent)
user = {"email": "ana@bakery.com", "password": "secret123"}
db.save(user)  # if someone reads your DB they have everyone's password
```

BCrypt is a one-way hash function. You store the hash, never the original password:

```
"secret123"  →  BCrypt.HashPassword()  →  "$2a$11$XKyT3..."
```

When the user logs in, you hash what they typed and compare the two hashes. You never reverse the hash.

| What you store | What you never store |
|---|---|
| `$2a$11$XKyT3t7...` (hash) | `secret123` (plaintext) |

---

## Step 1 — Install BCrypt

```bash
dotnet add package BCrypt.Net-Next --project src/Nastart.Api
```

---

## Step 2 — The RegisterCommand

Create `src/Nastart.Api/Features/Auth/Register.cs`:

```csharp
using BCrypt.Net;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Auth;

// ── Request ────────────────────────────────────────────────────────

public sealed record RegisterCommand(
    string Name,
    string Email,
    string Password,
    string? BusinessName,
    string? BusinessType     // "bakery" | "cafe" | "catering" | "home"
) : IRequest<RegisterResponse>;

// ── Response ───────────────────────────────────────────────────────

public sealed record RegisterResponse(
    Guid   UserId,
    string Name,
    string Email);

// ── Validator ──────────────────────────────────────────────────────

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()              // validates format: has @, has domain
            .MaximumLength(300);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)            // minimum security baseline
            .MaximumLength(100);         // cap so BCrypt doesn't get very long inputs

        RuleFor(x => x.BusinessType)
            .Must(t => t is null or "bakery" or "cafe" or "catering" or "home")
            .WithMessage("BusinessType must be one of: bakery, cafe, catering, home");
    }
}

// ── Handler ────────────────────────────────────────────────────────

public sealed class RegisterHandler(NastartDbContext db)
    : IRequestHandler<RegisterCommand, RegisterResponse>
{
    public async Task<RegisterResponse> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Check email is not already taken
        //    AnyAsync = SQL EXISTS — faster than loading the full user row
        var emailTaken = await db.Users
            .AnyAsync(u => u.Email == request.Email.ToLowerInvariant(), cancellationToken);

        if (emailTaken)
        {
            // Throw a domain exception — the endpoint catches it and returns 409
            throw new EmailAlreadyTakenException(request.Email);
        }

        // 2. Hash the password
        //    WorkFactor 11 = standard balance of security vs speed (~100ms per hash)
        //    Never use WorkFactor < 10 in production
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 11);

        // 3. Create the user
        var user = new User
        {
            Id           = Guid.NewGuid(),
            Name         = request.Name,
            Email        = request.Email.ToLowerInvariant(),   // always store lowercase
            PasswordHash = passwordHash,
            BusinessName = request.BusinessName,
            BusinessType = request.BusinessType,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return new RegisterResponse(user.Id, user.Name, user.Email);
    }
}

// ── Domain Exception ───────────────────────────────────────────────

public sealed class EmailAlreadyTakenException(string email)
    : Exception($"The email '{email}' is already registered.");
```

---

## Step 3 — The Endpoint

Create `src/Nastart.Api/Features/Auth/AuthEndpoints.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Nastart.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", async (
            RegisterCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            // 201 Created — resource was created, include its location
            return TypedResults.Created($"/auth/me", result);
        })
        .WithName("Register")
        .WithSummary("Create a new user account")
        .Produces<RegisterResponse>(201)
        .Produces<ProblemDetails>(400)   // validation errors
        .Produces<ProblemDetails>(409);  // email already taken

        return app;
    }
}
```

---

## Step 4 — Handle the 409 Conflict in the Global Exception Handler

You already have a global exception handler from Lesson 08 that catches `ValidationException`. Extend it to also handle `EmailAlreadyTakenException`:

Open `src/Nastart.Api/Shared/Behaviors/` or wherever your exception handler lives and add:

```csharp
// In your global exception handler (app.UseExceptionHandler or similar)
// Add this case alongside the existing ValidationException handling:

if (context.Exception is EmailAlreadyTakenException emailEx)
{
    context.Response.StatusCode = 409;   // Conflict
    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Title  = "Email already taken",
        Detail = emailEx.Message,
        Status = 409
    });
    context.ExceptionHandled = true;
    return;
}
```

If you're using the minimal API exception handler pattern from Lesson 08:

```csharp
// In Program.cs, extend the exception handler:
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionHandler = context.Features.Get<IExceptionHandlerFeature>();
        var ex = exceptionHandler?.Error;

        context.Response.ContentType = "application/problem+json";

        var problem = ex switch
        {
            ValidationException ve => new ProblemDetails
            {
                Title  = "Validation failed",
                Status = 400,
                Detail = string.Join("; ", ve.Errors.Select(e => e.ErrorMessage))
            },
            EmailAlreadyTakenException => new ProblemDetails   // ← new
            {
                Title  = "Email already taken",
                Status = 409,
                Detail = ex.Message
            },
            _ => new ProblemDetails
            {
                Title  = "An unexpected error occurred",
                Status = 500
            }
        };

        context.Response.StatusCode = problem.Status ?? 500;
        await context.Response.WriteAsJsonAsync(problem);
    });
});
```

---

## Step 5 — Register in Program.cs

```csharp
// Add to Program.cs after existing endpoint registrations
app.MapAuthEndpoints();    // ← new
app.MapIngredientsEndpoints();
```

---

## ✅ Test It

```http
### Register a new user
POST http://localhost:5000/auth/register
Content-Type: application/json

{
  "name": "Ana Bakery",
  "email": "ana@bakery.com",
  "password": "mysecret123",
  "businessName": "Ana's Bakery",
  "businessType": "bakery"
}
```

Expected responses:

```
201 Created        → { "userId": "...", "name": "Ana Bakery", "email": "ana@bakery.com" }
400 Bad Request    → password too short, invalid email format, invalid businessType
409 Conflict       → email already registered
```

Verify the row in PostgreSQL:

```sql
SELECT id, name, email, password_hash, business_type FROM users;
-- password_hash should start with $2a$ — that's the BCrypt prefix
-- it should NOT show "mysecret123"
```

---

## Key Concepts

| Term | Definition |
|---|---|
| **BCrypt** | One-way password hashing function. `HashPassword()` hashes. `Verify()` checks. Never reversible. |
| **Work factor** | Controls BCrypt cost. 11 = ~100ms per hash. Higher = slower to brute-force. |
| **`AnyAsync()`** | SQL `EXISTS` — returns true/false without loading the row. Faster than `FirstOrDefaultAsync()` for existence checks. |
| **`409 Conflict`** | HTTP status for "the resource already exists in the state you described" |
| **Lowercase email** | Always normalize to lowercase before storing and comparing — `Ana@Bakery.com` and `ana@bakery.com` are the same person |
| **`EmailAlreadyTakenException`** | Domain exception thrown in the handler, caught by the global exception handler |

---

## Next

Lesson 20 → **JWT Login** — the `users` table has a row. Now build `POST /auth/login` that verifies the password and returns a signed JWT token.
