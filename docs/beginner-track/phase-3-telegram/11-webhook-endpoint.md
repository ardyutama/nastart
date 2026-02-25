# Lesson 11: Webhook Endpoint

## 🎯 What you'll build
A `POST /telegram/webhook` endpoint that receives Telegram updates, routes them by message type and command, and sends a reply. `/start` and `/help` will work by the end of this lesson.

---

## 🔧 The .NET Way

### What is a Telegram Update?

Every message Telegram sends your webhook is called an **Update**. It's a big JSON object with one of many possible inner objects filled in:

```json
{
  "update_id": 100500,
  "message": {
    "message_id": 1365,
    "from": { "id": 1234567, "username": "ibu_sari" },
    "chat": { "id": 1234567, "type": "private" },
    "text": "/start"
  }
}
```

One Update could contain:
- `message` — a text or photo message
- `callback_query` — a button click from an inline keyboard
- `edited_message`, `inline_query`, etc.

The `Telegram.Bot` library has a `Update` C# class that matches this exactly.

### Exposing localhost to Telegram (ngrok)

Telegram can only call webhook URLs that are publicly accessible over HTTPS. During local development, ngrok creates a secure tunnel from the internet to your machine:

```
Telegram ──► https://xxxx.ngrok.io/telegram/webhook ──► Your localhost:7xxx
```

---

## Step 1 — Create the webhook feature

Create `src/Nastart.Api/Features/Bot/TelegramWebhook.cs`:

```csharp
using MediatR;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Nastart.Api.Features.Bot;

// ── Endpoint registration ──────────────────────────────────────────

public static class TelegramWebhookEndpoints
{
    public static WebApplication MapTelegramWebhookEndpoints(this WebApplication app)
    {
        // Telegram will POST to this URL
        app.MapPost("/telegram/webhook", HandleUpdate)
            .WithTags("Telegram");

        return app;
    }

    // Update update ← Telegram.Bot deserializes the JSON body to this type automatically
    static async Task<IResult> HandleUpdate(Update update, IMediator mediator)
    {
        // Dispatch to MediatR — the handler decides what to do
        await mediator.Send(new ProcessUpdateCommand(update));
        return TypedResults.Ok();
        // ↑ Always return 200 OK to Telegram, even if we don't handle this update type.
        //   If we return non-200, Telegram retries the same update for hours.
    }
}
```

---

## Step 2 — ProcessUpdateCommand (the router)

Create `src/Nastart.Api/Features/Bot/ProcessUpdate.cs`:

```csharp
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Nastart.Api.Features.Bot;

// ── Command ────────────────────────────────────────────────────────

public record ProcessUpdateCommand(Update Update) : IRequest;
// IRequest (no type parameter) = returns nothing (void)

// ── Handler (the router) ───────────────────────────────────────────

public class ProcessUpdateHandler(
    ITelegramBotClient bot,
    IMediator mediator)
    : IRequestHandler<ProcessUpdateCommand>
{
    public async Task Handle(
        ProcessUpdateCommand request,
        CancellationToken cancellationToken)
    {
        var update = request.Update;

        // Route based on update type
        // Telegram.Bot gives us a type-safe enum: UpdateType.Message, UpdateType.CallbackQuery...
        switch (update.Type)
        {
            case UpdateType.Message when update.Message?.Text is not null:
                await HandleTextMessage(update.Message, cancellationToken);
                break;

            // We'll add CallbackQuery (button clicks) in Lesson 13
            default:
                // Unknown/unhandled update type — safe to ignore
                break;
        }
    }

    private async Task HandleTextMessage(Message message, CancellationToken ct)
    {
        // Extract the command from the text (e.g., "/start" or "/ingredients flour")
        var text = message.Text!;                    // ! = we checked it's not null above
        var chatId = message.Chat.Id;
        var userId = message.From?.Id ?? chatId;     // From can be null in channels

        // Commands start with /
        // Split on space to separate command from arguments: "/cost Nastar" → ["/cost", "Nastar"]
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLower().TrimEnd('@');    // remove @bot_username suffix
        var args = parts.Length > 1 ? parts[1] : null;

        switch (command)
        {
            case "/start":
                await mediator.Send(new StartCommand(chatId, userId), ct);
                break;

            case "/help":
                await mediator.Send(new HelpCommand(chatId), ct);
                break;

            case "/ingredients":
                await mediator.Send(new IngredientsCommand(chatId), ct);
                break;

            case "/cost":
                await mediator.Send(new CostCommand(chatId, args), ct);
                break;

            default:
                // Unknown command — tell the user
                await bot.SendMessage(
                    chatId,
                    "Unknown command. Type /help to see what I can do.",
                    cancellationToken: ct);
                break;
        }
    }
}
```

---

## Step 3 — StartCommand and HelpCommand

Create `src/Nastart.Api/Features/Bot/StartCommand.cs`:

```csharp
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Nastart.Api.Features.Bot;

// ── /start command ─────────────────────────────────────────────────

public record StartCommand(long ChatId, long UserId) : IRequest;

public class StartCommandHandler(ITelegramBotClient bot)
    : IRequestHandler<StartCommand>
{
    public async Task Handle(StartCommand request, CancellationToken ct)
    {
        // MarkdownV2 lets us use bold (*text*), italic, etc.
        // Special characters need escaping in MarkdownV2: . ! ( ) [ ] etc → \. \! etc.
        var message = """
            👋 *Welcome to Nastart\!*

            I help you track ingredient prices and calculate recipe costs\.

            *Available commands:*
            /ingredients — List all ingredients with prices
            /cost — Calculate recipe cost \(e\.g\. `/cost Nastar`\)
            /help — Show this message
            """;

        await bot.SendMessage(
            request.ChatId,
            message,
            parseMode: ParseMode.MarkdownV2,    // enable markdown formatting
            cancellationToken: ct);
    }
}
```

Create `src/Nastart.Api/Features/Bot/HelpCommand.cs`:

```csharp
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Nastart.Api.Features.Bot;

public record HelpCommand(long ChatId) : IRequest;

public class HelpCommandHandler(ITelegramBotClient bot)
    : IRequestHandler<HelpCommand>
{
    public async Task Handle(HelpCommand request, CancellationToken ct)
    {
        const string message = """
            *Nastart Commands*

            /ingredients — Show all ingredients and current prices
            /cost \[recipe\] — Calculate cost breakdown for a recipe
            /help — Show this help message
            """;

        await bot.SendMessage(
            request.ChatId,
            message,
            parseMode: ParseMode.MarkdownV2,
            cancellationToken: ct);
    }
}
```

---

## Step 4 — Register the endpoint

Add to `Program.cs`:

```csharp
using Nastart.Api.Features.Bot;

// After app.MapIngredientsEndpoints():
app.MapTelegramWebhookEndpoints();
```

---

## Step 5 — Register the webhook URL with Telegram

**Start ngrok** (in a separate terminal):

```powershell
ngrok http 7xxx   # replace with your actual port from launchSettings.json
```

ngrok gives you a URL like `https://abc123.ngrok-free.app`. Copy it.

**Register the webhook** (replace with your ngrok URL and token):

```powershell
$token = "7123456789:AAHxxx..."
$url   = "https://abc123.ngrok-free.app/telegram/webhook"

Invoke-RestMethod `
    -Uri "https://api.telegram.org/bot$token/setWebhook?url=$url" `
    -Method Get
# Returns: { "ok": true, "result": true, "description": "Webhook was set" }
```

Now start the app and send `/start` to your bot in Telegram. You should get the welcome message back!

---

## ✅ Run it

```powershell
dotnet run
```

1. Send `/start` to your bot → welcome message
2. Send `/help` → command list
3. Send something random → "Unknown command" reply
4. Check the terminal — structured logs show `Handling ProcessUpdateCommand`

---

## 🔑 Key Concepts

| Concept | What it is |
|---------|-----------|
| `Update` | Telegram's message object — one per incoming event |
| `UpdateType` | Enum: Message, CallbackQuery, InlineQuery, etc. |
| Webhook vs Polling | Webhook: Telegram pushes to you. Always return 200 or Telegram retries. |
| ngrok | Tunnels localhost to a public HTTPS URL — for webhook dev |
| `setWebhook` | Telegram API call to register your webhook URL |
| `ParseMode.MarkdownV2` | Enables bold/italic in messages. Special chars need `\` escaping. |
| `SendMessage()` | Telegram.Bot method to send a text message to a chat |
| `IRequest` (no type) | MediatR command that returns nothing — like `async def handle(): void` |

---

➡️ Next: [Lesson 12 — /ingredients command](12-ingredients-command.md)
