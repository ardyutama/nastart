# Beginner Track: .NET 10 — Minimal API → MediatR → Telegram Bot

> **Who this is for**: Developers who know Python or JavaScript but are new to .NET/C#.  
> **What you build**: Nastart — a real inventory + pricing API for a bakery, then a Telegram bot on top.  
> **How it works**: One project. You keep adding to it, lesson by lesson.

---

## What You'll Build

By the end of this track you'll have a fully running app where:

1. A baker can call `POST /purchases` to record ingredients bought
2. `GET /recipes/{id}/cost` calculates real-time profit margins
3. A Telegram bot lets them type `/cost Nastar` and get the same answer instantly

---

## The Three Phases

```
Phase 1 — Minimal API CRUD        (Lessons 01–05)
  └─ Build the API with no abstractions. Just endpoints and a database.

Phase 2 — MediatR Refactor        (Lessons 06–09)
  └─ Refactor the same project. See exactly what problem MediatR solves.

Phase 3 — Telegram Bot            (Lessons 10–13)
  └─ Add a Telegram bot that uses the same MediatR handlers you already wrote.

Phase 4 — Database Mastery        (Lessons 14–17)
  └─ Normalize the schema, add indexes, write efficient queries, and build
     the real business queries that power Nastart's dashboard and alerts.

Phase 5 — Full Application        (Lessons 18–28)
  └─ Extend the project to the full 11-table Nastart schema. Add JWT auth,
     user-scoped ingredients, price history, recipes with costing, purchase
     recording with transactions, and a parallel-query dashboard.
```

---

## Tools to Install First

| Tool | Why | Install |
|------|-----|---------|
| [.NET 10 SDK](https://dotnet.microsoft.com/download) | The runtime + CLI | `winget install Microsoft.DotNet.SDK.10` |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Run PostgreSQL locally | Download from Docker website |
| [VS Code](https://code.visualstudio.com/) + [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) | Editor + IntelliSense | VS Code extensions panel |
| [git](https://git-scm.com/) | Version control | `winget install Git.Git` |
| [ngrok](https://ngrok.com/download) | Expose localhost to Telegram | Download from ngrok.com (free account) |

### Verify everything works

```powershell
dotnet --version    # should print 10.x.x
docker --version    # should print Docker version
git --version       # should print git version
```

---

## C# vs Python/JS — Quick Cheat Sheet

| Concept | Python | JavaScript | C# |
|---------|--------|------------|-----|
| Entry point | `if __name__ == "__main__"` | `index.js` | `Program.cs` |
| Package manager | `pip` + `requirements.txt` | `npm` + `package.json` | `dotnet add package` + `.csproj` |
| Data class | `@dataclass` | plain object | `record` |
| Async | `async def` + `await` | `async function` + `await` | `async Task` + `await` |
| Type hints | optional (PEP 484) | optional (TypeScript) | required |
| Null safety | `Optional[T]` or `T \| None` | `T \| null` | `T?` |
| Interface | ABC / Protocol | TypeScript `interface` | `interface` |
| Dependency injection | manual / frameworks | manual / frameworks | built-in to ASP.NET Core |

---

## Project Structure (what you'll end up with)

```
src/Nastart.Api/
├── Features/
│   ├── Auth/                          ← Phase 5
│   │   ├── Register.cs
│   │   ├── Login.cs
│   │   └── AuthEndpoints.cs
│   ├── Categories/                    ← Phase 5
│   ├── Dashboard/                     ← Phase 5
│   ├── Ingredients/
│   │   ├── Ingredient.cs              ← entity model
│   │   ├── PriceHistory.cs            ← Phase 5
│   │   ├── CreateIngredient.cs        ← MediatR command + handler (Phase 2)
│   │   ├── GetIngredients.cs          ← MediatR query + handler (Phase 2)
│   │   ├── UpdateIngredientPrice.cs   ← Phase 5
│   │   └── IngredientsEndpoints.cs    ← route registrations
│   ├── Recipes/                       ← Phase 5
│   └── Purchases/                     ← Phase 5
├── Shared/
│   ├── Config/
│   │   └── JwtOptions.cs              ← Phase 5
│   ├── Data/
│   │   └── NastartDbContext.cs
│   ├── Models/
│   │   └── PagedResult.cs             ← Phase 5
│   ├── Services/
│   │   ├── ICurrentUser.cs            ← Phase 5
│   │   └── CurrentUser.cs             ← Phase 5
│   └── Behaviors/
│       ├── ValidationBehavior.cs      ← Phase 2
│       └── LoggingBehavior.cs         ← Phase 2
├── Program.cs
└── Nastart.Api.csproj
```

---

## Lesson Index

| # | Lesson | Phase |
|---|--------|-------|
| [01](phase-1-crud/01-first-dotnet-project.md) | Create your first .NET project | 1 |
| [02](phase-1-crud/02-your-first-endpoint.md) | Your first Minimal API endpoint | 1 |
| [03](phase-1-crud/03-crud-in-memory.md) | Full CRUD with in-memory storage | 1 |
| [04](phase-1-crud/04-ef-core-postgres.md) | EF Core + PostgreSQL | 1 |
| [05](phase-1-crud/05-validation-openapi.md) | Validation + OpenAPI docs | 1 |
| [06](phase-2-mediatr/06-what-is-mediatr.md) | What is MediatR and why? | 2 |
| [07](phase-2-mediatr/07-first-command.md) | Your first Command and Query | 2 |
| [08](phase-2-mediatr/08-pipeline-behaviors.md) | Pipeline Behaviors | 2 |
| [09](phase-2-mediatr/09-refactor-all-features.md) | Refactor all features | 2 |
| [10](phase-3-telegram/10-create-your-bot.md) | Create your Telegram bot | 3 |
| [11](phase-3-telegram/11-webhook-endpoint.md) | Webhook endpoint | 3 |
| [12](phase-3-telegram/12-ingredients-command.md) | /ingredients command | 3 |
| [13](phase-3-telegram/13-cost-command.md) | /cost command + inline keyboard | 3 |
| [14](phase-4-database/14-normalization.md) | Database normalization — Nastart schema analysis | 4 |
| [15](phase-4-database/15-indexing.md) | Database indexing — which columns and why | 4 |
| [16](phase-4-database/16-efficient-queries.md) | Efficient LINQ and EF Core queries | 4 |
| [17](phase-4-database/17-business-queries.md) | Business queries — recipe cost, profit, dashboard | 4 |
| [18](phase-5-full-app/18-schema-migration.md) | Schema migration — users + categories tables | 5 |
| [19](phase-5-full-app/19-registration.md) | User registration with BCrypt | 5 |
| [20](phase-5-full-app/20-jwt-login.md) | JWT login — signing in and getting a token | 5 |
| [21](phase-5-full-app/21-protect-endpoints.md) | Protecting endpoints with ICurrentUser | 5 |
| [22](phase-5-full-app/22-ingredients-full-schema.md) | Ingredients full schema — user ownership + stock | 5 |
| [23](phase-5-full-app/23-price-history.md) | Price history + spike notifications | 5 |
| [24](phase-5-full-app/24-recipes.md) | Recipes — junction tables + status state machine | 5 |
| [25](phase-5-full-app/25-recipe-costing.md) | Recipe costing — cost + margin write-back | 5 |
| [26](phase-5-full-app/26-record-purchase.md) | Record purchase — atomic multi-table transaction | 5 |
| [27](phase-5-full-app/27-purchase-history.md) | Paginated purchase history with filters | 5 |
| [28](phase-5-full-app/28-dashboard.md) | Dashboard — parallel queries with Task.WhenAll | 5 |

---

📐 See the full database schema evolution: [schema-map.md](schema-map.md)

➡️ Start with [Lesson 01 — Create your first .NET project](phase-1-crud/01-first-dotnet-project.md)
