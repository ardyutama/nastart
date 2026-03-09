# Lesson 20: JWT Login — Signing In and Getting a Token

> **Phase**: 5 — Building the Full Application
> **Prerequisite**: Lesson 19. Registration works and the `users` table has rows.
> **What you build**: `POST /auth/login` — verifies email + password, returns a signed JWT.

---

## What You Will Learn

- What a JWT is and how it works
- How to generate a signed `JwtSecurityToken` in .NET
- What claims go inside the token (`sub`, `email`, `name`)
- How to configure the JWT signing key securely

---

## What Is a JWT?

A JWT (JSON Web Token) is a self-contained signed string that proves who you are. It has three parts separated by dots:

```
eyJhbGciOiJIUzI1NiJ9   .   eyJzdWIiOiJ1c2VyLWlkIn0   .   SflKxwRJSMeKKF2QT4fwpM
  ↑ Header (algorithm)       ↑ Payload (claims)              ↑ Signature
```

The payload contains **claims** — facts about the user:

```json
{
  "sub": "3fa85f64-5717-4562-b3fc-2c963f66afa6",   // user's id
  "email": "ana@bakery.com",
  "name": "Ana Bakery",
  "exp": 1772000000                                  // expiry timestamp
}
```

The signature is created using your **secret key**. Anyone can read the payload, but they cannot *forge* a token without the key. Your API verifies the signature on every request.

```
Python comparison:  similar to a session cookie, but stateless — the server stores nothing.
JS comparison:      similar to a signed JWT in `jsonwebtoken` npm package.
```

---

## Step 1 — Install the JWT Package

```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --project src/Nastart.Api
```

---

## Step 2 — Add JWT Config to appsettings

In `appsettings.Development.json`, add a `Jwt` section:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=nastart;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Key": "this-is-a-dev-secret-key-change-in-production-must-be-at-least-32-chars",
    "Issuer": "nastart-api",
    "Audience": "nastart-clients",
    "ExpiryDays": 7
  }
}
```

> **Production**: store the key in an environment variable or secrets manager — never commit a real secret to git.

Add a strongly-typed config class. Create `src/Nastart.Api/Shared/Config/JwtOptions.cs`:

```csharp
namespace Nastart.Api.Shared.Config;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Key        { get; init; }
    public required string Issuer     { get; init; }
    public required string Audience   { get; init; }
    public int             ExpiryDays { get; init; } = 7;
}
```

---

## Step 3 — The LoginCommand

Create `src/Nastart.Api/Features/Auth/Login.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nastart.Api.Shared.Config;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Auth;

// ── Request ────────────────────────────────────────────────────────

public sealed record LoginCommand(string Email, string Password)
    : IRequest<LoginResponse>;

// ── Response ───────────────────────────────────────────────────────

public sealed record LoginResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Name,
    string Email);

// ── Validator ──────────────────────────────────────────────────────

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

// ── Domain Exception ───────────────────────────────────────────────

public sealed class InvalidCredentialsException()
    : Exception("Email or password is incorrect.");

// ── Handler ────────────────────────────────────────────────────────

public sealed class LoginHandler(NastartDbContext db, IOptions<JwtOptions> jwtOptions)
    : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<LoginResponse> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find user by email (always compare lowercase)
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Email == request.Email.ToLowerInvariant(),
                cancellationToken);

        // 2. Always run BCrypt.Verify even if user is null
        //    This prevents timing attacks that reveal whether an email exists.
        //    BCrypt.Verify is slow — if we skip it for unknown emails, an attacker
        //    can detect which emails are registered by measuring response time.
        var passwordValid = user is not null
            && BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!passwordValid)
        {
            // Same error message regardless of whether email or password was wrong
            // Never tell the attacker which one failed
            throw new InvalidCredentialsException();
        }

        // 3. Build the JWT
        var token = GenerateToken(user!);

        return new LoginResponse(
            token.TokenString,
            token.ExpiresAt,
            user!.Id,
            user.Name,
            user.Email);
    }

    private (string TokenString, DateTimeOffset ExpiresAt) GenerateToken(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.ExpiryDays);

        // Claims = facts we embed in the token payload
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),   // subject = user id
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name,  user.Name),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()) // unique token id
        };

        // Signing key — must match what AddJwtBearer uses in Program.cs (Lesson 21)
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:   _jwt.Issuer,
            audience: _jwt.Audience,
            claims:   claims,
            expires:  expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
```

---

## Step 4 — Add Login Endpoint to AuthEndpoints

Open `src/Nastart.Api/Features/Auth/AuthEndpoints.cs` and add the login route:

```csharp
group.MapPost("/login", async (
    LoginCommand command,
    IMediator mediator,
    CancellationToken ct) =>
{
    var result = await mediator.Send(command, ct);
    return TypedResults.Ok(result);
})
.WithName("Login")
.WithSummary("Sign in and receive a JWT token")
.Produces<LoginResponse>(200)
.Produces<ProblemDetails>(400)   // missing fields
.Produces<ProblemDetails>(401);  // wrong credentials
```

---

## Step 5 — Handle 401 in the Global Exception Handler

Add `InvalidCredentialsException` to your exception handler switch expression:

```csharp
InvalidCredentialsException => new ProblemDetails
{
    Title  = "Invalid credentials",
    Status = 401,
    Detail = "Email or password is incorrect."
},
```

---

## Step 6 — Register JwtOptions in Program.cs

```csharp
// Add before var app = builder.Build();
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));
```

---

## ✅ Test It

```http
### Login
POST http://localhost:5000/auth/login
Content-Type: application/json

{
  "email": "ana@bakery.com",
  "password": "mysecret123"
}
```

Expected responses:

```
200 OK → {
  "token": "eyJhbGci...",
  "expiresAt": "2026-03-16T...",
  "userId": "3fa85f64-...",
  "name": "Ana Bakery",
  "email": "ana@bakery.com"
}

401 Unauthorized → wrong email or password
400 Bad Request  → missing fields
```

**Decode the token** at [jwt.io](https://jwt.io) — paste the token and inspect the payload. You should see `sub`, `email`, and `name` claims.

---

## Key Concepts

| Term | Definition |
|---|---|
| **JWT** | Signed token with user claims. Stateless — server stores nothing. |
| **Claim** | A key-value fact inside the JWT payload (`sub` = user id, `email`, `name`) |
| **`sub` claim** | Subject — the user's ID. Conventionally used as the primary identifier. |
| **Symmetric key** | The same key signs and verifies the token. Only your server has it. |
| **`HMACSHA256`** | Signing algorithm — fast, secure for symmetric keys. |
| **Timing attack** | An attack that measures response time to detect which emails are registered. Mitigated by always running BCrypt.Verify. |
| **`JwtSecurityTokenHandler`** | .NET class that serializes the token to the signed string format. |
| **7-day expiry** | No refresh tokens in Phase 5. User logs in again after 7 days. |

---

## Next

Lesson 21 → **Protecting Endpoints** — you have a token. Now configure ASP.NET Core to validate it on every request, extract the `userId` from it, and wire it into all your MediatR handlers via `ICurrentUser`.
