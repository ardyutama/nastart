# Week 1: Strategic Design 🎯

> **Goal**: Before writing any code, we need to understand WHAT we're building and WHY.

---

## Table of Contents
1. [Day 1-2: Event Storming](#day-1-2-event-storming)
2. [Day 3: Bounded Contexts](#day-3-bounded-contexts)
3. [Day 4: Ubiquitous Language](#day-4-ubiquitous-language)
4. [Day 5: GitHub Repository Setup](#day-5-github-repository-setup)
5. [Day 6: .NET Solution Structure (slnx)](#day-6-net-solution-structure-slnx)
6. [Day 7: Docker & PostgreSQL](#day-7-docker--postgresql)
7. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1-2: Event Storming

## 🧒 Explain Like I'm 5

Imagine you're telling a story about a baker named Ibu Sari:

> "Ibu Sari **went to the store**. She **bought flour**. She **took a photo** of the receipt. The app **saw the photo**. The app **wrote down** what she bought. The flour **got more expensive**! The app **told Ibu Sari** about the price change."

See those **bold words**? Those are **things that happened** — we call them "events"!

Event Storming is like telling the whole story of your app using sticky notes. Each sticky note = one thing that happened.

**Orange sticky note** = Something happened (event)
**Blue sticky note** = A command (someone did something)
**Yellow sticky note** = Who/what did it

## 🔧 Engineer Language

**Event Storming** is a collaborative workshop technique created by Alberto Brandolini to discover business domains through domain events.

### Core Concepts:

| Color | Element | Description |
|-------|---------|-------------|
| 🟠 Orange | Domain Event | Something that happened (past tense): `PurchaseRecorded`, `PriceChanged` |
| 🔵 Blue | Command | Action that triggers event: `RecordPurchase`, `ScanReceipt` |
| 🟡 Yellow | Aggregate | Entity that handles command: `Purchase`, `Ingredient` |
| 🟣 Purple | Policy | Automated reaction: "When price spikes, send alert" |
| 🔴 Red | Hot Spot | Questions, problems, unclear areas |
| 🟢 Green | Read Model | Data needed for UI/queries |

### How to Do It:

1. **Chaotic Exploration** (30 min)
   - Write ALL events you can think of on orange sticky notes
   - Don't organize yet, just brainstorm

2. **Timeline** (20 min)
   - Arrange events left-to-right in time order
   - Start: User registers → End: User sees profit report

3. **Find Pivotal Events** (10 min)
   - Events that change the system state significantly
   - `PurchaseRecorded`, `RecipeCostCalculated`, `AlertSent`

4. **Add Commands & Aggregates** (30 min)
   - What command triggered each event?
   - What entity processed it?

### Nastart Event Storm Example:

```
Timeline →

[User Registered] → [Phone Linked] → [Receipt Photo Sent] → [Receipt Scanned] 
    → [Items Extracted] → [Ingredients Matched] → [Purchase Recorded] 
    → [Prices Updated] → [Price Spike Detected] → [Alert Sent]
    
                                    ↓
                            [Recipe Created] → [Cost Calculated] 
                            → [Margin Analyzed] → [Margin Alert Sent]
```

### Your Task (Day 1-2):

1. Get paper/whiteboard or use Miro/FigJam
2. Write these events on orange notes:
   - `UserRegistered`
   - `PhoneVerified`
   - `ReceiptPhotoReceived`
   - `ReceiptScanned`
   - `PurchaseRecorded`
   - `IngredientPriceUpdated`
   - `PriceSpikeDetected`
   - `RecipeCreated`
   - `RecipeCostCalculated`
   - `MarginBelowThreshold`
   - `AlertCreated`
   - `AlertSent`
   - `SaleRecorded`

3. Arrange in timeline
4. Add commands (blue) that trigger each event
5. Circle related events that belong together

---

# Day 3: Bounded Contexts

## 🧒 Explain Like I'm 5

Imagine your house has different rooms:
- **Kitchen** = where you cook
- **Bedroom** = where you sleep
- **Bathroom** = where you shower

Each room has its own rules! You don't sleep in the kitchen, right?

In our app, we have "rooms" too:
- **Inventory Room** = knows about ingredients and stock
- **Recipe Room** = knows how to make cookies
- **Money Room** = knows about buying and selling
- **Alert Room** = knows when to tell you something important

Each "room" only cares about its own stuff. The Recipe Room doesn't care HOW you bought the flour — it just needs to know the flour price!

## 🔧 Engineer Language

**Bounded Context** is a central pattern in Domain-Driven Design (DDD). It defines a boundary within which a particular domain model applies.

### Why Bounded Contexts Matter:

1. **Clear Ownership** — Each context has its own code, database tables, team
2. **Independent Deployment** — Change one context without breaking others
3. **Focused Models** — `Ingredient` in Inventory ≠ `Ingredient` in Recipe context
4. **Reduced Complexity** — Smaller, understandable pieces

### Nastart Bounded Contexts:

```
┌─────────────────────────────────────────────────────────────────┐
│                        NASTART SYSTEM                           │
├─────────────────┬─────────────────┬─────────────────┬──────────┤
│   📦 INVENTORY  │   📖 RECIPE     │   💰 FINANCE    │ 🔔 ALERT │
├─────────────────┼─────────────────┼─────────────────┼──────────┤
│ • Ingredient    │ • Recipe        │ • Purchase      │ • Alert  │
│ • Category      │ • RecipeItem    │ • PurchaseItem  │ • Trigger│
│ • Shop          │ • CostCalc      │ • Sale          │ • Channel│
│ • Stock         │ • Margin        │ • PriceHistory  │          │
├─────────────────┼─────────────────┼─────────────────┼──────────┤
│ Responsibilities│ Responsibilities│ Responsibilities│ Respons. │
│ - Track stock   │ - Store recipes │ - Log purchases │ - Create │
│ - Manage shops  │ - Calculate cost│ - Track prices  │   alerts │
│ - Categorize    │ - Analyze margin│ - Record sales  │ - Send   │
│                 │                 │ - Detect spikes │   notifs │
└─────────────────┴─────────────────┴─────────────────┴──────────┘
```

### Context Mapping (How They Talk):

```
Finance ──(PriceChanged)──► Inventory
         │
         └─(PriceSpikeDetected)──► Alert

Recipe ──(MarginBelowThreshold)──► Alert

Inventory ──(LowStock)──► Alert
```

### Integration Patterns:

| Pattern | Use Case |
|---------|----------|
| **Domain Events** | Finance publishes `PriceChanged`, Inventory subscribes |
| **Shared Kernel** | Common Value Objects like `Money`, `Quantity` |
| **Anti-Corruption Layer** | External APIs (WhatsApp, OCR) don't leak into domain |

### Your Task (Day 3):

1. Draw 4 boxes on paper — one for each context
2. List entities/concepts in each box
3. Draw arrows showing which events flow between contexts
4. Ask yourself: "If I change X in Context A, does Context B care?"

---

# Day 4: Ubiquitous Language

## 🧒 Explain Like I'm 5

When mom says "snack time!", everyone knows it means cookies and milk. Nobody is confused!

But what if dad calls it "eating time" and grandma calls it "treat time"? Confusing, right?

**Ubiquitous Language** means EVERYONE uses the SAME words for the SAME things:
- We ALL say "Ingredient" (not "item", "product", "material")
- We ALL say "Purchase" (not "transaction", "buy", "order")
- We ALL say "Margin" (not "profit percent", "markup")

When the baker says "margin" and the programmer says "margin" — they mean the SAME thing!

## 🔧 Engineer Language

**Ubiquitous Language** is a shared vocabulary between developers and domain experts, used consistently in code, documentation, and conversation.

### Benefits:

1. **No Translation Layer** — Code reads like business requirements
2. **Reduced Miscommunication** — "Recipe" in meeting = `Recipe` in code
3. **Living Documentation** — Code IS the specification
4. **Onboarding** — New devs learn domain by reading code

### Nastart Glossary:

| Term | Definition | Code Example |
|------|------------|--------------|
| **Ingredient** | A trackable item used in recipes | `class Ingredient` |
| **Purchase** | A single shopping transaction with receipt | `class Purchase` |
| **Recipe** | A product formula with ingredients and quantities | `class Recipe` |
| **RecipeItem** | One ingredient entry in a recipe with quantity | `class RecipeItem` |
| **Cost** | Total ingredient expense to produce one unit | `Money TotalCost` |
| **Margin** | Percentage profit after deducting cost from sell price | `Percentage Margin` |
| **Price Spike** | Ingredient price increases >15% from last purchase | `PriceSpikeDetectedEvent` |
| **Low Stock** | Ingredient quantity below minimum threshold | `LowStockEvent` |
| **Shop** | A store where ingredients are purchased | `class Shop` |
| **Category** | Classification of ingredients (dairy, dry goods, etc.) | `class Category` |

### Anti-Patterns to Avoid:

```csharp
// ❌ BAD — Generic programming terms
class ItemManager { }
class TransactionService { }
class DataProcessor { }

// ✅ GOOD — Domain language
class Ingredient { }
class PurchaseService { }
class RecipeCostCalculator { }
```

### How Ubiquitous Language Appears in Code:

```csharp
// The code reads like a business conversation!
public class Recipe
{
    public void AddIngredient(Ingredient ingredient, Quantity quantity)
    {
        var recipeItem = new RecipeItem(ingredient, quantity);
        _items.Add(recipeItem);
        RecalculateCost();
    }
    
    public void RecalculateCost()
    {
        TotalCost = _items.Sum(item => item.CalculateCost());
        Margin = CalculateMargin(SellPrice, TotalCost);
        
        if (Margin.IsBelow(_minimumMargin))
        {
            AddDomainEvent(new MarginBelowThresholdEvent(this));
        }
    }
}
```

### Your Task (Day 4):

1. Create a glossary document (already exists: `docs/glossary.md`)
2. Define 15-20 key terms
3. Review with stakeholder (or imagine explaining to a baker)
4. Ensure EVERY term maps to a class/property/method name

---

# Day 5: GitHub Repository Setup

## 🧒 Explain Like I'm 5

GitHub is like a magical notebook that:
- **Remembers everything** you write
- **Lets friends help** you write together
- **Never loses** your work (even if your computer breaks!)
- **Shows history** — what you wrote yesterday, last week, last year

A "repository" (repo) is ONE notebook for ONE project.

A "monorepo" is a BIG notebook with different sections:
- Section 1: Backend (the brain)
- Section 2: Frontend (the face)
- Section 3: Docs (the instructions)

## 🔧 Engineer Language

### Monorepo Structure for Nastart:

```
nastart/                          # Root repository
├── .github/
│   └── workflows/
│       └── deploy.yml            # CI/CD pipeline
├── backend/                      # .NET 10 solution
│   ├── Nastart.slnx              # New XML solution format!
│   ├── src/
│   └── tests/
├── frontend/                     # Nuxt 4 application
│   ├── nuxt.config.ts
│   └── ...
├── ocr-service/                  # Python PaddleOCR microservice
│   ├── main.py
│   └── requirements.txt
├── docs/                         # Documentation
├── docker-compose.yml            # Local development
├── .gitignore
├── README.md
└── LICENSE
```

### Commands to Execute:

```powershell
# Navigate to your projects folder
cd C:\Users\AU1833\Documents\personal

# Create the repository (if not exists)
mkdir nastart
cd nastart

# Initialize git
git init

# Create folder structure
mkdir backend, frontend, ocr-service, docs
mkdir docs\lessons

# Create essential files
New-Item README.md
New-Item LICENSE
New-Item docker-compose.yml
New-Item .gitignore

# Create GitHub repository and push
gh repo create nastart --private --source=. --push
```

### .gitignore for Nastart:

```gitignore
# .NET
bin/
obj/
*.user
*.suo
.vs/

# Node
node_modules/
.nuxt/
.output/
dist/

# Python
__pycache__/
*.pyc
venv/
.env

# IDE
.idea/
*.swp

# OS
.DS_Store
Thumbs.db

# Secrets
appsettings.*.json
!appsettings.json
!appsettings.Development.json
```

### Your Task (Day 5):

1. Create GitHub account (if needed)
2. Install GitHub CLI: `winget install GitHub.cli`
3. Create repository with folder structure
4. Write initial README.md
5. Push to GitHub

---

# Day 6: .NET Solution Structure (slnx)

## 🧒 Explain Like I'm 5

Imagine you're building with LEGO:
- **Solution (.slnx)** = The instruction book for the whole LEGO city
- **Project** = One building (house, school, hospital)
- **Reference** = A bridge connecting buildings

Our LEGO city (Nastart) has these buildings:
- `Nastart.Api` — The front door (where visitors come in)
- `Nastart.Domain` — The rules book (what can and can't happen)
- `Nastart.Application` — The manager (tells everyone what to do)
- `Nastart.Infrastructure` — The workers (talk to database, send messages)
- `Nastart.Bot` — The messenger (talks to WhatsApp/Telegram)

## 🔧 Engineer Language

### Clean Architecture Layers:

```
┌──────────────────────────────────────────────────────────────┐
│                         Nastart.Api                          │
│  (Controllers, Middleware, Program.cs)                       │
│  Depends on: Application, Infrastructure                     │
├──────────────────────────────────────────────────────────────┤
│                      Nastart.Bot                             │
│  (Telegram/WhatsApp handlers)                                │
│  Depends on: Application, Infrastructure                     │
├──────────────────────────────────────────────────────────────┤
│                   Nastart.Application                        │
│  (Commands, Queries, Handlers, DTOs)                         │
│  Depends on: Domain                                          │
├──────────────────────────────────────────────────────────────┤
│                  Nastart.Infrastructure                      │
│  (EF Core, Repositories, External Services)                  │
│  Depends on: Domain, Application                             │
├──────────────────────────────────────────────────────────────┤
│                     Nastart.Domain                           │
│  (Entities, Value Objects, Domain Events, Interfaces)        │
│  Depends on: NOTHING (pure domain logic)                     │
└──────────────────────────────────────────────────────────────┘
```

### Dependency Rule:

> **Inner layers NEVER depend on outer layers.**
> Domain is the core — it knows nothing about databases, HTTP, or UI.

### Why `.slnx` over `.sln`?

| Aspect | `.sln` (Legacy) | `.slnx` (.NET 10 default) |
|--------|-----------------|---------------------------|
| Format | Proprietary text | Clean XML |
| Readability | Cryptic GUIDs | Human-readable |
| Git Merge | Conflict-prone | Easy to merge |
| Size | Verbose | Compact |

> **Note**: In .NET 10, `dotnet new sln` automatically creates `.slnx` format. No extra flags needed!
> If you need the legacy `.sln` format, use: `dotnet new sln --format sln`

### Commands to Create Solution:

> ✅ **Verified with .NET 10.0.101** — All commands below are from official Microsoft documentation.
> See: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend

# Create solution (.slnx is the default format in .NET 10)
dotnet new sln -n Nastart

# Create projects using official templates
# Template: webapi - ASP.NET Core Web API (controllers or minimal APIs)
# Template: classlib - Class Library
# Template: xunit - xUnit Test Project
# See all templates: dotnet new list

dotnet new webapi -n Nastart.Api -o src/Nastart.Api
dotnet new classlib -n Nastart.Domain -o src/Nastart.Domain
dotnet new classlib -n Nastart.Application -o src/Nastart.Application
dotnet new classlib -n Nastart.Infrastructure -o src/Nastart.Infrastructure
dotnet new classlib -n Nastart.Bot -o src/Nastart.Bot

# Create test projects
dotnet new xunit -n Nastart.Domain.Tests -o tests/Nastart.Domain.Tests
dotnet new xunit -n Nastart.Application.Tests -o tests/Nastart.Application.Tests

# Add projects to solution
# See: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln
dotnet sln add src/Nastart.Api/Nastart.Api.csproj
dotnet sln add src/Nastart.Domain/Nastart.Domain.csproj
dotnet sln add src/Nastart.Application/Nastart.Application.csproj
dotnet sln add src/Nastart.Infrastructure/Nastart.Infrastructure.csproj
dotnet sln add src/Nastart.Bot/Nastart.Bot.csproj
dotnet sln add tests/Nastart.Domain.Tests/Nastart.Domain.Tests.csproj
dotnet sln add tests/Nastart.Application.Tests/Nastart.Application.Tests.csproj

# Add project references
# See: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-add-reference
dotnet add src/Nastart.Application/Nastart.Application.csproj reference src/Nastart.Domain/Nastart.Domain.csproj
dotnet add src/Nastart.Infrastructure/Nastart.Infrastructure.csproj reference src/Nastart.Domain/Nastart.Domain.csproj
dotnet add src/Nastart.Infrastructure/Nastart.Infrastructure.csproj reference src/Nastart.Application/Nastart.Application.csproj
dotnet add src/Nastart.Api/Nastart.Api.csproj reference src/Nastart.Application/Nastart.Application.csproj
dotnet add src/Nastart.Api/Nastart.Api.csproj reference src/Nastart.Infrastructure/Nastart.Infrastructure.csproj
dotnet add src/Nastart.Bot/Nastart.Bot.csproj reference src/Nastart.Application/Nastart.Application.csproj
dotnet add src/Nastart.Bot/Nastart.Bot.csproj reference src/Nastart.Infrastructure/Nastart.Infrastructure.csproj

# Add test references
dotnet add tests/Nastart.Domain.Tests/Nastart.Domain.Tests.csproj reference src/Nastart.Domain/Nastart.Domain.csproj
dotnet add tests/Nastart.Application.Tests/Nastart.Application.Tests.csproj reference src/Nastart.Application/Nastart.Application.csproj
```

### What Nastart.slnx Looks Like:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/Nastart.Api/Nastart.Api.csproj" />
    <Project Path="src/Nastart.Domain/Nastart.Domain.csproj" />
    <Project Path="src/Nastart.Application/Nastart.Application.csproj" />
    <Project Path="src/Nastart.Infrastructure/Nastart.Infrastructure.csproj" />
    <Project Path="src/Nastart.Bot/Nastart.Bot.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/Nastart.Domain.Tests/Nastart.Domain.Tests.csproj" />
    <Project Path="tests/Nastart.Application.Tests/Nastart.Application.Tests.csproj" />
  </Folder>
</Solution>
```

See how clean that is? No random GUIDs, no cryptic syntax!

### Verify Solution:

```powershell
dotnet build
dotnet test
```

---

# Day 7: Docker & PostgreSQL

## 🧒 Explain Like I'm 5

Docker is like a **lunchbox** for computer programs:
- Your sandwich (program) stays fresh
- It doesn't mix with your friend's sandwich
- You can share the exact same lunchbox with anyone!

PostgreSQL is a **super-smart filing cabinet** that:
- Remembers ALL your data
- Finds things super fast
- Never forgets (even if you turn off the computer)

We put PostgreSQL INSIDE a Docker lunchbox, so everyone on our team has the exact same filing cabinet!

## 🔧 Engineer Language

### Why Docker for Local Dev:

1. **Consistent Environment** — Works the same on every machine
2. **No Installation Hassle** — No "it works on my machine"
3. **Easy Cleanup** — Delete container, fresh start
4. **Multiple Versions** — Run PostgreSQL 18 alongside other versions

### docker-compose.yml:

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:18
    container_name: nastart-db
    environment:
      POSTGRES_USER: nastart
      POSTGRES_PASSWORD: nastart_dev_password
      POSTGRES_DB: nastart
    ports:
      - "5432:5432"
    volumes:
      - nastart_postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U nastart"]
      interval: 10s
      timeout: 5s
      retries: 5

  # PaddleOCR service for development
  ocr:
    build: ./ocr-service
    container_name: nastart-ocr
    ports:
      - "8001:8001"
    environment:
      - PYTHONUNBUFFERED=1

volumes:
  nastart_postgres_data:
```

### Commands to Run:

```powershell
# Install Docker Desktop for Windows (if not installed)
winget install Docker.DockerDesktop

# Start services
cd C:\Users\AU1833\Documents\personal\nastart
docker-compose up -d

# Check status
docker-compose ps

# View logs
docker-compose logs -f postgres

# Connect to PostgreSQL
docker exec -it nastart-db psql -U nastart -d nastart

# Stop services
docker-compose down

# Stop and remove data
docker-compose down -v
```

### Connection String for .NET:

```json
// appsettings.Development.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=nastart;Username=nastart;Password=nastart_dev_password"
  }
}
```

### Install EF Core Tools:

```powershell
dotnet tool install --global dotnet-ef
```

### Your Task (Day 7):

1. Install Docker Desktop
2. Create `docker-compose.yml` in project root
3. Run `docker-compose up -d`
4. Verify PostgreSQL is running
5. Update connection string in appsettings

---

# Resources

> ✅ All links verified as of January 2026

## Event Storming
| Resource | Type | Link |
|----------|------|------|
| Event Storming (Alberto Brandolini) | Book | https://www.eventstorming.com/book/ |
| Event Storming Guide | Article | https://www.eventstorming.com/ |
| Miro Event Storming Template | Tool | https://miro.com/templates/event-storming/ |

## Domain-Driven Design (Microsoft Official)
| Resource | Type | Link |
|----------|------|------|
| **Design a DDD-oriented microservice** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/ddd-oriented-microservice |
| **Using domain analysis to model microservices** | MS Docs | https://learn.microsoft.com/en-us/azure/architecture/microservices/model/domain-analysis |
| **Implement a microservice domain model with .NET** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/net-core-microservice-domain-model |
| **Design a microservice domain model** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model |
| Vaughn Vernon - Effective Aggregate Design (Part 1-3) | PDF | https://dddcommunity.org/library/vernon_2011/ |
| DDD Reference (Eric Evans - Free PDF) | PDF | https://www.domainlanguage.com/ddd/reference/ |

## Clean Architecture (Microsoft Official)
| Resource | Type | Link |
|----------|------|------|
| **Common web application architectures - Clean Architecture** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures#clean-architecture |
| **Design a microservice-oriented application** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/microservice-application-design |
| Ardalis Clean Architecture Template | GitHub | https://github.com/ardalis/cleanarchitecture |
| Jason Taylor Clean Architecture | GitHub | https://github.com/jasontaylordev/CleanArchitecture |

## .NET 10 & CLI (Microsoft Official)
| Resource | Type | Link |
|----------|------|------|
| **dotnet new command** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new |
| **Default templates for dotnet new** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new-sdk-templates |
| **dotnet sln command** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln |
| **dotnet add reference** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-add-reference |
| **What's new in .NET 10** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview |
| **dotnet new sln defaults to SLNX** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/10.0/dotnet-new-sln-slnx-default |
| **What's new in MSBuild 17 (SLNX support)** | MS Docs | https://learn.microsoft.com/en-us/visualstudio/msbuild/whats-new-msbuild-17-0 |

## Entity Framework Core & PostgreSQL (Microsoft Official)
| Resource | Type | Link |
|----------|------|------|
| **Entity Framework Core** | MS Docs | https://learn.microsoft.com/en-us/ef/core/ |
| **Getting Started with EF Core** | MS Docs | https://learn.microsoft.com/en-us/ef/core/get-started/overview/first-app |
| **EF Core - ASP.NET MVC Tutorial** | MS Docs | https://learn.microsoft.com/en-us/aspnet/core/data/ef-mvc/intro |
| **Razor Pages with EF Core** | MS Docs | https://learn.microsoft.com/en-us/aspnet/core/data/ef-rp/intro |
| Npgsql EF Core Provider | Docs | https://www.npgsql.org/efcore/ |

## Docker & Containers (Microsoft Official)
| Resource | Type | Link |
|----------|------|------|
| **Introduction to .NET and Docker** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/docker/introduction |
| **Tutorial: Containerize a .NET app** | MS Docs | https://learn.microsoft.com/en-us/dotnet/core/docker/build-container |
| **Use a database server running as a container** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/database-server-container |
| **Development workflow for Docker apps** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/docker-application-development-process/docker-app-development-workflow |
| Docker Desktop | Tool | https://docs.docker.com/desktop/ |

## CQRS & MediatR (Microsoft Official)
| Resource | Type | Link |
|----------|------|------|
| **Implement the command process pipeline with MediatR** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-application-layer-implementation-web-api |
| **Domain events: design and implementation** | MS Docs | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation |
| MediatR Library | GitHub | https://github.com/jbogard/MediatR |

## eBooks (Free Downloads from Microsoft)
| Resource | Description | Link |
|----------|-------------|------|
| **.NET Microservices Architecture** | Complete DDD/CQRS guide | https://learn.microsoft.com/en-us/dotnet/architecture/microservices/ |
| **Architect Modern Web Apps with ASP.NET Core** | Clean Architecture patterns | https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/ |

## Tools
| Tool | Purpose | Link |
|------|---------|------|
| Miro | Event Storming online | https://miro.com |
| FigJam | Collaborative whiteboard | https://www.figma.com/figjam/ |
| draw.io | Diagrams | https://www.drawio.com/ |
| GitHub Desktop | Git GUI | https://desktop.github.com/ |
| DBeaver | PostgreSQL GUI | https://dbeaver.io/ |
| pgAdmin | PostgreSQL Admin | https://www.pgadmin.org/ |

---

# Week 1 Checklist

- [ ] **Day 1-2**: Complete Event Storming exercise
  - [ ] Write 15+ domain events on sticky notes
  - [ ] Arrange in timeline
  - [ ] Identify commands and aggregates
  - [ ] Take photo/screenshot for reference

- [ ] **Day 3**: Define Bounded Contexts
  - [ ] Draw 4 context boxes
  - [ ] Assign entities to each context
  - [ ] Map events between contexts

- [ ] **Day 4**: Write Ubiquitous Language
  - [ ] Create/update `docs/glossary.md`
  - [ ] Define 15-20 terms
  - [ ] Verify terms match code naming

- [ ] **Day 5**: GitHub Repository
  - [ ] Create monorepo structure
  - [ ] Write README.md
  - [ ] Add .gitignore
  - [ ] Push to GitHub

- [ ] **Day 6**: .NET Solution (slnx)
  - [ ] Create solution with `--format slnx`
  - [ ] Add 5 projects + 2 test projects
  - [ ] Add project references
  - [ ] Verify `dotnet build` works

- [ ] **Day 7**: Docker & PostgreSQL
  - [ ] Install Docker Desktop
  - [ ] Create docker-compose.yml
  - [ ] Start PostgreSQL container
  - [ ] Verify connection

---

**Next Week**: [Week 2 - Domain Layer Foundation](./week-02-domain-foundation.md)

---

## Quick Reference: Key Microsoft Docs

For quick access during development, bookmark these essential Microsoft docs:

```
.NET CLI Commands:
├── dotnet new: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new
├── dotnet sln: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-sln
├── dotnet add reference: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-add-reference
└── All templates: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-new-sdk-templates

DDD & Microservices Architecture:
└── https://learn.microsoft.com/en-us/dotnet/architecture/microservices/

.NET 10 What's New:
└── https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/overview

EF Core Getting Started:
└── https://learn.microsoft.com/en-us/ef/core/get-started/overview/first-app

Docker + .NET:
└── https://learn.microsoft.com/en-us/dotnet/core/docker/introduction
```

### Available Templates (dotnet new list)

| Template | Short Name | Description |
|----------|------------|-------------|
| Solution File | `sln` | Empty solution (slnx in .NET 10) |
| ASP.NET Core Web API | `webapi` | Web API with controllers or minimal APIs |
| Class Library | `classlib` | Reusable class library |
| Console App | `console` | Console application |
| xUnit Test Project | `xunit` | Unit test project with xUnit |
| MSTest Test Project | `mstest` | Unit test project with MSTest |
| NUnit Test Project | `nunit` | Unit test project with NUnit |
| Blazor Web App | `blazor` | Interactive web UI with Blazor |
| Worker Service | `worker` | Background worker service |

Run `dotnet new list` to see all available templates on your system.

---

*Happy learning! Remember: understanding WHY is more important than HOW.* 🚀
