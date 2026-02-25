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
│   ├── Ingredients/
│   │   ├── Ingredient.cs              ← entity model
│   │   ├── CreateIngredient.cs        ← MediatR command + handler (Phase 2)
│   │   ├── GetIngredients.cs          ← MediatR query + handler (Phase 2)
│   │   └── IngredientsEndpoints.cs    ← route registrations
│   ├── Recipes/
│   └── Purchases/
├── Shared/
│   ├── Data/
│   │   └── NastartDbContext.cs
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

---

➡️ Start with [Lesson 01 — Create your first .NET project](phase-1-crud/01-first-dotnet-project.md)
