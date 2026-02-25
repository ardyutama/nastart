# Lesson 10: Create Your Telegram Bot

## 🎯 What you'll build
Create a Telegram bot via BotFather, install the `Telegram.Bot` NuGet package, and configure the bot token securely so the app can authenticate with Telegram's API.

---

## 🔧 How Telegram Bots Work

A Telegram bot is just an HTTP client that talks to `https://api.telegram.org`. Telegram sends your app incoming messages via one of two mechanisms:

| Method | How it works | Best for |
|--------|-------------|---------|
| **Polling** | Your app repeatedly asks "any new messages?" | Local dev without ngrok |
| **Webhook** | Telegram pushes messages to your URL | Production (what we use) |

We use webhooks because they're lower latency and scale better. Telegram calls `POST /telegram/webhook` on your server when a user sends a message.

```
User sends message
      │
      ▼
Telegram servers
      │  POST /telegram/webhook
      ▼
Your .NET API
      │  mediator.Send(command)
      ▼
MediatR Handler
      │  Telegram.Bot client
      ▼
Telegram sends reply to user
```

---

## Step 1 — Create your bot via BotFather

1. Open Telegram, search for **@BotFather**
2. Send `/newbot`
3. Enter a display name: **Nastart Bot**
4. Enter a username: **nastart_dev_bot** (must end in `bot`, must be unique)
5. BotFather replies with a token like:
   ```
   7123456789:AAHxxx...yyy
   ```

**Configure the bot commands** (send to BotFather):
```
/setcommands
→ select your bot
→ paste this:
ingredients - List all ingredients
cost - Calculate recipe cost
price - Check ingredient price
help - Show available commands
```

---

## Step 2 — Install Telegram.Bot

```powershell
cd src/Nastart.Api
dotnet add package Telegram.Bot
```

`Telegram.Bot` is the official .NET client for the Telegram Bot API. It handles authentication, JSON serialization, and provides typed models for all Telegram object types.

---

## Step 3 — Store the token securely

**Never commit your bot token to git.**

Add to `appsettings.Development.json` (this file should be in `.gitignore`):

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=nastart;Username=nastart;Password=nastart_dev"
  },
  "Telegram": {
    "BotToken": "7123456789:AAHxxx...paste-your-token-here"
  }
}
```

Create a strongly-typed options class for the configuration:

Create `src/Nastart.Api/Shared/Configuration/TelegramOptions.cs`:

```csharp
namespace Nastart.Api.Shared.Configuration;

/// <summary>
/// Strongly-typed configuration for Telegram bot settings.
/// Bound from appsettings.json "Telegram" section.
/// </summary>
public class TelegramOptions
{
    // Convention: section name matches class name without "Options"
    public const string SectionName = "Telegram";

    public required string BotToken { get; init; }
    // init = can only be set during object initialization (immutable after construction)
}
```

---

## Step 4 — Register the Telegram client in Program.cs

```csharp
using Nastart.Api.Shared.Configuration;
using Telegram.Bot;

// Add after the other builder.Services registrations:

// Bind TelegramOptions from appsettings.json
builder.Services.Configure<TelegramOptions>(
    builder.Configuration.GetSection(TelegramOptions.SectionName)
);
// ↑ IOptions<TelegramOptions> is now injectable anywhere in the app
// Compare: Python's pydantic Settings or Node's dotenv

// Register ITelegramBotClient as a singleton (one instance per app lifetime)
builder.Services.AddSingleton<ITelegramBotClient>(provider =>
{
    // Resolve the options we just bound
    var options = provider
        .GetRequiredService<IOptions<TelegramOptions>>()
        .Value;

    return new TelegramBotClient(options.BotToken);
});
// ↑ AddSingleton = one instance shared across all requests
// Compare: AddScoped = new instance per request, AddTransient = new instance every time
```

Add the missing `using` at the top of `Program.cs`:
```csharp
using Microsoft.Extensions.Options;
```

---

## Step 5 — Verify the token works

Add a temporary test endpoint (we'll remove it in Lesson 11):

```csharp
// Temporary — add after app.MapGet("/", ...)
app.MapGet("/bot-info", async (ITelegramBotClient bot) =>
{
    // GetMeAsync() calls the Telegram API: GET /bot{token}/getMe
    var me = await bot.GetMeAsync();
    return new { me.Id, me.Username, me.FirstName };
});
```

```powershell
dotnet run
```

```powershell
curl https://localhost:7xxx/bot-info
# Returns: { "id": 7123456789, "username": "nastart_dev_bot", "firstName": "Nastart Bot" }
```

If you see your bot's info, the token is working. Remove the test endpoint before Lesson 11.

---

## ✅ Checkpoint

- [ ] Bot created and token saved in `appsettings.Development.json`
- [ ] `dotnet add package Telegram.Bot` succeeded
- [ ] `GET /bot-info` returns your bot's username
- [ ] `.gitignore` excludes `appsettings.Development.json` (check with `git status`)

---

## 🔑 Key Concepts

| Concept | What it is |
|---------|-----------|
| Webhook vs Polling | Webhook = Telegram pushes to you. Polling = you ask Telegram. Use webhooks. |
| `ITelegramBotClient` | The main interface for calling the Telegram Bot API |
| `GetMeAsync()` | API call to verify the token and get bot info |
| `Configure<T>()` | Bind a config section to a typed options class |
| `IOptions<T>` | Inject configuration options into any class |
| `AddSingleton` | One instance for the app lifetime — right for HTTP clients |
| `init` property | Can only be set during initialization — like `frozen=True` in Python dataclass |

---

➡️ Next: [Lesson 11 — Webhook endpoint](11-webhook-endpoint.md)
