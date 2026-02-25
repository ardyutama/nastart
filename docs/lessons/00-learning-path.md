# Nastart — Complete Learning Path 🗺️

> **How to build the app step-by-step, from zero to a fully working product.**

This document is your single source of truth for the build sequence.
Read it before starting any week so you always know what you're building and why.

---

## The Full Picture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        BUILD SEQUENCE                                │
├──────────┬──────────────────────────────────────────────────────────┤
│  Week 1  │  Plan what to build — VSA, GitHub, Docker, .NET solution │
│  Week 2  │  Foundation — folder structure, entities, pipeline       │
│  Week 3  │  Purchase domain — Record receipts, price tracking       │
│  Week 4  │  Recipe domain — Costs, margins, alerts                  │
│  Week 5  │  Database — EF Core configs, migrations, seeding         │
│  Week 6  │  API Polish — OpenAPI, health checks, CORS, errors       │
│  Week 7  │  OCR Service — PaddleOCR microservice, ScanReceipt      │
│  Week 8  │  Telegram bot — Webhook, /start, /help, routing         │
│  Week 9  │  Receipt flow — Photo → OCR → confirm → save            │
│  Week 10 │  Bot commands + alerts — /cost, /price, /profit         │
│  Week 11 │  Containers — Dockerfiles, Docker Compose, production   │
└──────────┴──────────────────────────────────────────────────────────┘
```

---

## Why This Order?

### The Golden Rule
> **Only depend on things already built.**  
> Never write a handler before its pipeline, never call a service before it exists.

### Dependency Graph

```
Week 1: Project scaffold
  └── Week 2: Shared infrastructure (DbContext, MediatR pipeline, Result type)
        ├── Week 3: Purchase features (depend on Ingredient entity from Week 2)
        ├── Week 4: Recipe features (depend on Ingredient + Purchase entities)
        └── Week 5: Database finalization (migrations after ALL entities defined)
              └── Week 6: API polish (after all endpoints exist)
                    └── Week 7: OCR integration (enriches the API)
                          └── Week 8: Telegram bot (uses the polished API + OCR)
                                ├── Week 9: Receipt photo flow (bot + OCR combined)
                                └── Week 10: Bot commands & alerts (full system)
                                      └── Week 11: Containerize everything
```

---

## Week-by-Week Guide

---

### Week 1 — Strategic Design 🎯
**File**: [week-01-strategic-design.md](week-01-strategic-design.md)

| Day | Topic | Outcome |
|-----|-------|---------|
| 1–2 | Feature discovery & user stories | Feature list defined |
| 3   | Vertical Slice Architecture concepts | Understand WHY we structure code this way |
| 4   | Feature glossary & naming conventions | Shared vocabulary for the whole project |
| 5   | GitHub repo setup + `.gitignore` | Remote repo with proper exclusions |
| 6   | .NET solution structure (slnx) | `Nastart.Api` + `Nastart.Api.Tests` projects |
| 7   | Docker + PostgreSQL with best practices | Database running locally |

**What you can run at the end of this week**: `docker compose up` → PostgreSQL is up, .NET project compiles.

---

### Week 2 — Domain Foundation & Building Blocks 🏗️
**File**: [week-02-domain-foundation.md](week-02-domain-foundation.md)

| Day | Topic | Outcome |
|-----|-------|---------|
| 1   | Feature folder structure | `Features/Ingredients/`, `Shared/` scaffolded |
| 2   | Core entity models (Ingredient, User) | C# entity classes |
| 3   | C# Records for feature DTOs | `Result<T>`, request/response types |
| 4   | MediatR notifications for side effects | Publish-subscribe pattern for domain events |
| 5   | **MediatR pipeline behaviors** ⚠️ | `ValidationBehavior` + `LoggingBehavior` |
| 6   | The feature slice pattern + `CreateIngredient` | First working feature end-to-end |
| 7   | Testing feature slices | Unit tests for handlers |

> ⚠️ **Important**: Day 5 (pipeline behaviors) is currently documented in  
> [week-05-database-shared-services.md → Day 5 & Day 6](week-05-database-shared-services.md).  
> **Read those sections here, during Week 2**, before you build your first handler.  
> Every handler you write from Week 2 onwards will flow through these behaviors.

**What you can run at the end of this week**: `POST /ingredients` creates a real ingredient in PostgreSQL.

---

### Week 3 — Purchase Domain 💰
**File**: [week-03-purchase-domain.md](week-03-purchase-domain.md)

| Day | Topic | Outcome |
|-----|-------|---------|
| 1   | Purchase + PurchaseItem entity models | C# entity classes |
| 2   | `RecordPurchase` feature slice | Save a receipt to the database |
| 3   | `GetPurchaseHistory` query | List purchases for a user |
| 4   | Price change detection logic | Detect when an ingredient's price changes |
| 5   | `PriceChangedNotification` flow | MediatR notification → update ingredient price |
| 6   | `PriceSpikeNotification` + alert logic | Trigger alerts on large price jumps |
| 7   | Testing purchase features | Unit + integration tests |

> **Note**: EF Core entity configurations for Purchase & PurchaseItem are covered in  
> [week-05-database-shared-services.md → Day 1 & Day 2](week-05-database-shared-services.md).  
> You can reference those configs now or apply them in Week 5.

**What you can run at the end of this week**: `POST /purchases` saves a full receipt. Price history is tracked.

---

### Week 4 — Recipe Domain 🍳
**File**: [week-04-recipe-domain.md](week-04-recipe-domain.md)

| Day | Topic | Outcome |
|-----|-------|---------|
| 1   | Recipe + RecipeItem entity models | C# entity classes |
| 2   | `CreateRecipe` feature slice | Create a recipe |
| 3   | `AddIngredientToRecipe` feature | Add ingredients with quantities |
| 4   | `GetRecipeCost` query (live calculation) | Real-time cost from current ingredient prices |
| 5   | Margin calculation + threshold detection | Profit margin logic |
| 6   | `MarginBelowThresholdNotification` | Alert when a recipe becomes unprofitable |
| 7   | Testing recipe features | Unit + integration tests |

**What you can run at the end of this week**: `GET /recipes/{id}/cost` returns a live cost breakdown + margin.

---

### Week 5 — Database & Shared Services 🗄️
**File**: [week-05-database-shared-services.md](week-05-database-shared-services.md)

| Day | Topic | Outcome |
|-----|-------|---------|
| 1   | EF Core entity configurations (Ingredient, Category) | `IEntityTypeConfiguration<T>` classes |
| 2   | Relationship mappings (Purchase ↔ Items, Recipe ↔ Items) | FK constraints, cascade rules |
| 3   | Database migrations | First `dotnet ef migrations add InitialCreate` |
| 4   | Data seeding (units of measure, categories) | Seed data on startup |
| 5   | `ValidationBehavior` pipeline *(reference, built in Week 2)* | FluentValidation wired into MediatR |
| 6   | `LoggingBehavior` + exception handling *(reference, built in Week 2)* | Structured logs for every request |
| 7   | Testing database + behaviors | Verify EF configs + behavior tests |

> ℹ️ Days 5–6 of this week are **prerequisites for Week 2**.  
> If you are reading this week in sequence, treat Days 5–6 as a review and  
> make sure those behaviors were already built before you wrote your first handler.

**What you can run at the end of this week**: `dotnet ef database update` → all tables created, seed data applied.

---

### Week 6 — API Polish & OpenAPI 🎨
**File**: [week-06-api-polish-openapi.md](week-06-api-polish-openapi.md)

| Day | Topic | Outcome |
|-----|-------|---------|
| 1   | OpenAPI / Swagger configuration | Interactive API docs at `/swagger` |
| 2   | Request/response examples & schemas | Rich documentation with examples |
| 3   | Health check endpoints | `/health` for readiness + liveness |
| 4   | CORS configuration for the frontend | Frontend can call the API |
| 5   | Rate limiting for bot endpoints | Prevent abuse of webhook endpoint |
| 6   | Global error handling middleware | Consistent `ProblemDetails` error responses |
| 7   | API testing + documentation review | Validate all endpoints are documented |

> **Why before OCR/Bot?** Polish the API surface first so the bot and OCR service  
> connect to a stable, well-documented foundation. Easier to debug later.

**What you can run at the end of this week**: Browse `/swagger` — all endpoints documented. `/health` returns 200.

---

### Week 7 — PaddleOCR Integration 📷
**File**: [week-07-ocr-integration.md](week-07-ocr-integration.md)

| Day | Topic | Outcome |
|-----|-------|---------|
| 1   | PaddleOCR Python microservice setup | `ocr-service/` directory, virtual env |
| 2   | FastAPI receipt scanning endpoint | `POST /scan` accepts image → returns text |
| 3   | `PaddleOcrService` HTTP client in .NET | Typed `HttpClient` that calls OCR service |
| 4   | `ScanReceipt` feature slice | Full feature: image in → purchase items out |
| 5   | `FuzzyMatchingService` for ingredients | Map OCR text → known ingredient names |
| 6   | Error handling + retry policies | Polly retry on OCR timeouts |
| 7   | Testing with real receipts | End-to-end OCR pipeline test |

**What you can run at the end of this week**: `POST /receipts/scan` (with an image) → returns matched ingredient lines.

---

### Week 8 — Telegram Bot Fundamentals 🤖
**File**: [week-08-bot-fundamentals.md](week-08-bot-fundamentals.md)

| Day | Topic | Outcome |
|-----|-------|---------|
| 1   | Create bot via BotFather | Bot token secured in `appsettings` |
| 2   | Telegram Bot API — webhooks & message types | Understand Update → Message → Text/Photo |
| 3   | `TelegramWebhookEndpoint` feature | `POST /telegram/webhook` registered |
| 4   | `StartCommand` feature slice | `/start` returns welcome message  |
| 5   | `HelpCommand` feature slice | `/help` returns command list |
| 6   | Command routing & dispatcher | Route incoming commands to the right handler |
| 7   | Testing bot features | Mock Telegram updates in tests |

**What you can run at the end of this week**: Send `/start` to the bot → it replies. `/help` lists all commands.

---

### Week 9 — Receipt Photo Handling 📸
**File**: [week-09-receipt-photo-handling.md](week-09-receipt-photo-handling.md)

| Day | Topic | Outcome |
|-----|-------|---------|
| 1   | `PhotoHandler` feature slice | Catches photo messages in the webhook |
| 2   | Download image from Telegram | `GetFile` API → download bytes |
| 3   | Connect to `ScanReceipt` feature | Photo → OCR → parsed items |
| 4   | Build confirmation keyboard UI | Inline keyboard with ✅/❌ per item |
| 5   | Handle confirmation callbacks | `callback_query` → save or discard |
| 6   | Progress updates + error handling | "Processing…" → results → confirm |
| 7   | End-to-end photo flow test | Full journey: photo in → data saved |

**What you can run at the end of this week**: Send a receipt photo to the bot → it reads it and asks for confirmation → you confirm → data is saved.

---

### Week 10 — Bot Commands + Alerts 📢
**File**: [week-10-bot-commands-alerts.md](week-10-bot-commands-alerts.md)

| Day | Topic | Outcome |
|-----|-------|---------|
| 1   | `CostCommand` (`/cost [recipe]`) | Returns cost breakdown + margin |
| 2   | `PriceCommand` (`/price [ingredient]`) | Returns current price + history |
| 3   | `LowStockCommand` (`/low`) | Lists ingredients below threshold |
| 4   | `ProfitCommand` (`/profit`) | Summary of all recipe margins |
| 5   | Telegram alert notifications | Push alerts for price spikes + margin drops |
| 6   | Notification handlers + scheduling | Background trigger for daily summaries |
| 7   | Full bot workflow testing | All commands tested end-to-end |

**What you can run at the end of this week**: The **complete app** works end-to-end via Telegram.

---

### Week 11 — Containerization 🐳
**File**: [week-11-containerization.md](week-11-containerization.md)

| Day | Topic | Outcome |
|-----|-------|---------|
| 1   | Docker fundamentals for Nastart | Understand containers vs images |
| 2   | .NET API Dockerfile (multi-stage) | Minimal production image |
| 3   | PaddleOCR service Dockerfile | GPU-capable Python image |
| 4   | Nuxt 4 frontend Dockerfile | Static build served by Nginx |
| 5   | Docker Compose orchestration | All services start with `docker compose up` |
| 6   | Production hardening & security | Non-root users, secrets, health checks |
| 7   | Review & practice | Full deployment on a single machine |

**What you can run at the end of this week**: `docker compose up` starts everything. The app is fully deployed.

---

## Common Prerequisites by Week

| Before starting | Make sure you have |
|-----------------|--------------------|
| Week 2 | `docker compose up` shows PostgreSQL healthy |
| Week 3 | `POST /ingredients` works end-to-end |
| Week 4 | `POST /purchases` saves a purchase |
| Week 5 | All three entity files (Ingredient, Purchase, Recipe) are created |
| Week 6 | `dotnet ef database update` succeeds with no errors |
| Week 7 | All API endpoints return consistent `ProblemDetails` on errors |
| Week 8 | `POST /receipts/scan` with a test image returns parsed lines |
| Week 9 | `/start` command reply works |
| Week 10 | Receipt photo flow runs end-to-end |
| Week 11 | Full Telegram workflow works locally |

---

## Key Architecture Decisions (Quick Reference)

| Decision | Choice | Why |
|----------|--------|-----|
| API style | Minimal APIs | Less boilerplate, feature-slice friendly |
| Code organization | Vertical Slice Architecture | Feature isolation, easy navigation |
| Mediator | MediatR | Decouples endpoints from business logic |
| Validation | FluentValidation + pipeline behavior | Automatic, centralized validation |
| Database | EF Core + PostgreSQL | Type-safe queries, migrations |
| OCR | PaddleOCR (Python microservice) | Best open-source Indonesian text support |
| Bot channel | Telegram webhooks | Low latency, no polling |
| Containers | Docker + Docker Compose | Reproducible across dev/prod |

---

## File Index (Current on Disk)

| File | Content |
|------|---------|
| [week-01-strategic-design.md](week-01-strategic-design.md) | Week 1 |
| [week-02-domain-foundation.md](week-02-domain-foundation.md) | Week 2 |
| [week-03-purchase-domain.md](week-03-purchase-domain.md) | Week 3 |
| [week-04-recipe-domain.md](week-04-recipe-domain.md) | Week 4 |
| [week-05-database-shared-services.md](week-05-database-shared-services.md) | Week 5 — Days 5–6 belong in Week 2 |
| [week-06-api-polish-openapi.md](week-06-api-polish-openapi.md) | Week 6 |
| [week-07-ocr-integration.md](week-07-ocr-integration.md) | Week 7 |
| [week-08-bot-fundamentals.md](week-08-bot-fundamentals.md) | Week 8 |
| [week-09-receipt-photo-handling.md](week-09-receipt-photo-handling.md) | Week 9 |
| [week-10-bot-commands-alerts.md](week-10-bot-commands-alerts.md) | Week 10 |
| [week-11-containerization.md](week-11-containerization.md) | Week 11 |
