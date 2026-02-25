# Lesson 01: Create Your First .NET 10 Project

## 🎯 What you'll build
A runnable .NET 10 Web API project — the skeleton that all future lessons will build on.

---

## 🔧 The .NET Way

### The CLI — dotnet

Just like Python has `python` and Node has `node`, .NET has the `dotnet` CLI. You use it to create projects, add packages, run, test, and build.

```powershell
dotnet --version    # 10.x.x
```

### Project templates

`dotnet new` creates a project from a template — similar to `create-react-app` or `cookiecutter`.

| Template flag | What it creates |
|---------------|-----------------|
| `webapi` | HTTP API (what we want) |
| `console` | Console app |
| `classlib` | Reusable library |

### .csproj — the project file

Equivalent to `package.json` or `requirements.txt`. It lists your NuGet packages (like npm packages / pip packages) and the target framework.

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
```

---

## Step 1 — Create the project

```powershell
# Navigate to your workspace
cd C:\Users\AU1833\Documents\personal\nastart

# Create the solution folder
mkdir nastart
cd nastart

# Create the API project using Minimal APIs (--use-minimal-apis skips Controller scaffolding)
dotnet new webapi -o src/Nastart.Api --use-minimal-apis

# Open in VS Code
code .
```

You'll see this structure generated:

```
src/Nastart.Api/
├── Properties/
│   └── launchSettings.json    ← local dev URLs and ports
├── appsettings.json           ← app configuration (like .env but JSON)
├── appsettings.Development.json
├── Program.cs                 ← entry point — everything starts here
└── Nastart.Api.csproj         ← project file (NuGet packages live here)
```

---

## Step 2 — Understand Program.cs

Open `Program.cs`. It was generated with a weather forecast example — we'll delete that, but first understand it:

```csharp
// Program.cs (generated — read first, then replace)

var builder = WebApplication.CreateBuilder(args);
// ↑ Like FastAPI's `app = FastAPI()` or Express's `const app = express()`
// This sets up the dependency injection container, configuration, logging.

builder.Services.AddOpenApi();
// ↑ Registers OpenAPI documentation. Services is the DI container.
// Compare: FastAPI has this built in. Express needs swagger-jsdoc.

var app = builder.Build();
// ↑ Locks in all registrations and creates the app.

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // ↑ Only expose /openapi/v1.json in dev, not production.
}

app.UseHttpsRedirection();
// ↑ Redirect http:// → https:// automatically.

// The generated weather endpoint — we'll replace this.
var summaries = new[] { "Freezing", "Cool", "Warm", "Hot" };
app.MapGet("/weatherforecast", () => { /* ... */ });

app.Run();
// ↑ Start the server and block until stopped. Like uvicorn.run() or app.listen().
```

---

## Step 3 — Replace with a clean Program.cs

Replace the entire file content with:

```csharp
// Program.cs
// ═══════════════════════════════════════════════════════
// This is the application entry point.
// In .NET, top-level statements let you write code
// directly without wrapping it in a class or method.
// ═══════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

// Register OpenAPI (Swagger) documentation
builder.Services.AddOpenApi();

var app = builder.Build();

// Only expose the OpenAPI endpoint in development
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Placeholder — we'll add real endpoints in Lesson 02
app.MapGet("/", () => "Nastart API is running!");

app.Run();
```

---

## Step 4 — Run it

```powershell
cd src/Nastart.Api
dotnet run
```

You should see:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:7xxx
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5xxx
```

Open your browser at `http://localhost:5xxx` — you'll see: `Nastart API is running!`

---

## Step 5 — Add Scalar UI (better than Swagger default)

Scalar is a modern OpenAPI UI. It's nicer than the default SwaggerUI.

```powershell
dotnet add package Scalar.AspNetCore
```

Update `Program.cs`:

```csharp
using Scalar.AspNetCore;        // ← add this using statement at the top

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();  // ← adds /scalar/v1 UI
}

app.UseHttpsRedirection();
app.MapGet("/", () => "Nastart API is running!");
app.Run();
```

Restart and visit `https://localhost:7xxx/scalar/v1` — you'll see the interactive API docs UI.

---

## ✅ Checkpoint

- [ ] `dotnet run` works without errors
- [ ] `GET /` returns `Nastart API is running!`
- [ ] `/scalar/v1` opens the API docs UI

---

## 🔑 Key Concepts

| Concept | What it is |
|---------|-----------|
| `dotnet new webapi` | Scaffolds a new Web API project |
| `Program.cs` | Application entry point, all startup config here |
| `builder.Services` | The dependency injection container — register things here |
| `builder.Build()` | Freezes registrations, creates the app |
| `app.Map*()` | Registers a route/endpoint |
| `app.Run()` | Starts the HTTP server |
| `appsettings.json` | Config file — like `.env` but JSON, checked into git |
| `appsettings.Development.json` | Overrides for local dev only — like `.env.local` |
| NuGet | .NET package registry — like PyPI / npm |
| `dotnet add package` | Install a NuGet package — like `pip install` / `npm install` |

---

➡️ Next: [Lesson 02 — Your first Minimal API endpoint](02-your-first-endpoint.md)
