# Lesson 21: Protecting Endpoints — Validating JWTs on Every Request

> **Phase**: 5 — Building the Full Application
> **Prerequisite**: Lesson 20. You can call `POST /auth/login` and receive a token.
> **What you build**: Every ingredient endpoint requires a valid JWT. A new `ICurrentUser` service extracts the logged-in user's ID from the token automatically.

---

## What You Will Learn

- How ASP.NET Core validates JWTs on every request
- How to create an `ICurrentUser` service that reads claims from the token
- How to scope ingredient queries to the logged-in user

---

## How JWT Validation Works

When you add `AddJwtBearer(...)`, ASP.NET Core does this on every request automatically:

```
Request arrives → Authorization: Bearer eyJhbGci...
       ↓
Middleware validates signature, issuer, audience, expiry
       ↓
OK: ClaimsPrincipal is populated in HttpContext.User
       ↓
Your code can read HttpContext.User.FindFirst("sub") → userId
```

If validation fails (expired, tampered, missing), it returns `401 Unauthorized` **before** your handler runs.

---

## Step 1 — Configure Authentication in Program.cs

Add these two blocks before `var app = builder.Build()`:

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

// 1. Tell ASP.NET Core how to validate tokens
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()!;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtOptions.Issuer,
            ValidAudience            = jwtOptions.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew                = TimeSpan.Zero   // no grace period on expiry
        };
    });

builder.Services.AddAuthorization();

// 2. Register ICurrentUser so handlers can use it
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
```

After `var app = builder.Build()`, add the middleware in this exact order:

```csharp
app.UseAuthentication();   // validates the token
app.UseAuthorization();    // checks [Authorize] attributes
```

> **Order matters**: `UseAuthentication` must come before `UseAuthorization`, and both must come before `app.MapXxx()` calls.

---

## Step 2 — Create ICurrentUser

Create `src/Nastart.Api/Shared/Services/ICurrentUser.cs`:

```csharp
namespace Nastart.Api.Shared.Services;

public interface ICurrentUser
{
    Guid UserId { get; }
}
```

Create `src/Nastart.Api/Shared/Services/CurrentUser.cs`:

```csharp
using System.Security.Claims;

namespace Nastart.Api.Shared.Services;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            // The "sub" claim was set in LoginHandler.GenerateToken
            var sub = httpContextAccessor.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier);

            // This should never happen for [Authorize] endpoints —
            // ASP.NET Core blocks unauthenticated requests before we even reach here.
            // The guard is here as a safety net for misconfigured routes.
            return sub is not null
                ? Guid.Parse(sub)
                : throw new UnauthorizedAccessException("No authenticated user.");
        }
    }
}
```

> **Note on claims**: `JwtRegisteredClaimNames.Sub` and `ClaimTypes.NameIdentifier` map to the same underlying value — ASP.NET Core maps `sub` to `NameIdentifier` automatically when using `AddJwtBearer`.

---

## Step 3 — Protect the Ingredients Route Group

Open `src/Nastart.Api/Features/Ingredients/IngredientsEndpoints.cs`. Apply `RequireAuthorization()` to the entire route group:

```csharp
public static void MapIngredientsEndpoints(this WebApplication app)
{
    var group = app.MapGroup("/ingredients")
        .RequireAuthorization()       // ← every ingredient endpoint now requires a valid JWT
        .WithTags("Ingredients");

    group.MapGet("/", ...);
    group.MapGet("/{id}", ...);
    group.MapPost("/", ...);
    // ...
}
```

---

## Step 4 — Scope GetIngredients to the Current User

Now that ingredients belong to a user, `GET /ingredients` should only return the caller's ingredients.

Open `src/Nastart.Api/Features/Ingredients/GetIngredients.cs`. Update the query and handler:

```csharp
// Before (returns all ingredients for everyone)
public sealed record GetIngredientsQuery() : IRequest<List<IngredientResponse>>;

// After (scoped to the logged-in user)
public sealed record GetIngredientsQuery(Guid UserId) : IRequest<List<IngredientResponse>>;
```

Update the handler's `Handle` method:

```csharp
public async Task<List<IngredientResponse>> Handle(
    GetIngredientsQuery request,
    CancellationToken cancellationToken)
{
    return await db.Ingredients
        .AsNoTracking()
        .Where(i => i.UserId == request.UserId)    // ← scoped to caller
        .Select(i => new IngredientResponse(i.Id, i.Name, i.Unit, i.PricePerUnit))
        .ToListAsync(cancellationToken);
}
```

Update the endpoint to pass in the current user:

```csharp
group.MapGet("/", async (
    IMediator mediator,
    ICurrentUser currentUser,
    CancellationToken ct) =>
{
    var result = await mediator.Send(new GetIngredientsQuery(currentUser.UserId), ct);
    return TypedResults.Ok(result);
});
```

Apply the same `UserId` scoping pattern to `GetIngredientById`, `CreateIngredient`, `UpdateIngredient`, and `DeleteIngredient`. Each handler now accepts `UserId` from `ICurrentUser`, and the endpoints inject `ICurrentUser currentUser` and pass `currentUser.UserId` into the command/query.

---

## ✅ Test It

```http
### 1. Login first (copy the token)
POST http://localhost:5000/auth/login
Content-Type: application/json

{
  "email": "ana@bakery.com",
  "password": "mysecret123"
}

### 2. Use the token to get ingredients
GET http://localhost:5000/ingredients
Authorization: Bearer eyJhbGci...paste-your-token...
```

Expected responses:

```
Without token:
401 Unauthorized

With expired or tampered token:
401 Unauthorized

With valid token:
200 OK → [ ... your ingredients only ... ]
```

---

## How to Read the Auth Header in .http Files

Most .http client tools (VS Code REST Client, JetBrains HTTP Client) support variables:

```http
@token = eyJhbGci...

GET http://localhost:5000/ingredients
Authorization: Bearer {{token}}
```

---

## Key Concepts

| Term | Definition |
|---|---|
| **`AddJwtBearer`** | Configures ASP.NET Core to validate the `Authorization: Bearer <token>` header on every request |
| **`ClaimsPrincipal`** | The object that holds all claims from the validated JWT. Lives in `HttpContext.User`. |
| **`ICurrentUser`** | Your custom service that extracts `UserId` from the claims — injected via DI into any handler |
| **`RequireAuthorization()`** | Applied to a route group. Any request without a valid JWT returns 401 before reaching your code. |
| **`ClockSkew = TimeSpan.Zero`** | Disables the default 5-minute grace period on token expiry. Tokens expire exactly when they should. |
| **User scoping** | `WHERE user_id = @current_user_id` — ensures each user only sees their own data |

---

## Next

Lesson 22 → **Ingredients Full Schema** — the `ingredients` table gains `user_id`, `category_id`, `stock_quantity`, and `reorder_threshold`. You will write an additive migration and update the existing ingredient handlers to manage the new fields.
