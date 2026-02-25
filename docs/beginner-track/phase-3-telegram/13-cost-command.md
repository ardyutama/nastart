# Lesson 13: /cost Command + Inline Keyboard

## 🎯 What you'll build
A `/cost [recipe name]` command that calculates a recipe's cost breakdown from live ingredient prices and shows the result with an inline keyboard offering "🔄 Recalculate" and "📋 All Recipes" buttons.

---

## Prerequisites

This lesson assumes you have `Recipe` and `RecipeItem` entities and a `GetRecipeCostQuery` MediatR handler. If you haven't built those yet, the pattern and bot wiring are exactly the same as the Ingredients example — build the domain first, then add the bot command.

For reference, the recipe cost query signature looks like:

```csharp
public record GetRecipeCostQuery(string RecipeName) : IRequest<RecipeCostResult?>;

public record RecipeCostResult(
    string RecipeName,
    decimal TotalCost,
    decimal SellingPrice,
    decimal MarginPercent,
    List<CostLineItem> Items
);

public record CostLineItem(string IngredientName, decimal Quantity, string Unit, decimal LineCost);
```

---

## 🔧 Inline Keyboards

An **inline keyboard** is a set of buttons attached to a message. When a user taps a button, Telegram sends your webhook a `callback_query` update (not a message):

```json
{
  "callback_query": {
    "id": "abc123",
    "from": { "id": 1234567 },
    "message": { "message_id": 42, "chat": { "id": 1234567 } },
    "data": "recalculate:Nastar"    ← the data string you defined
  }
}
```

Your handler calls `AnswerCallbackQuery()` to dismiss the spinner on the button, then does its work.

---

## Step 1 — Create the CostCommand

Create `src/Nastart.Api/Features/Bot/CostCommand.cs`:

```csharp
using MediatR;
using Nastart.Api.Features.Recipes;    // GetRecipeCostQuery lives here
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Nastart.Api.Features.Bot;

// ── Command ────────────────────────────────────────────────────────

/// <summary>
/// Handles /cost [recipe name] — shows cost breakdown with action buttons.
/// </summary>
public record CostCommand(long ChatId, string? RecipeName) : IRequest;

// ── Handler ────────────────────────────────────────────────────────

public class CostCommandHandler(
    ITelegramBotClient bot,
    IMediator mediator)
    : IRequestHandler<CostCommand>
{
    public async Task Handle(CostCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RecipeName))
        {
            // User typed /cost without an argument
            await bot.SendMessage(
                request.ChatId,
                "Usage: `/cost RecipeName`\nExample: `/cost Nastar`",
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: ct);
            return;
        }

        // Show a typing indicator while we calculate
        await bot.SendChatAction(request.ChatId, ChatAction.Typing, cancellationToken: ct);

        // Reuse the domain query — same one the REST API /recipes/{name}/cost uses
        var result = await mediator.Send(new GetRecipeCostQuery(request.RecipeName), ct);

        if (result is null)
        {
            await bot.SendMessage(
                request.ChatId,
                $"Recipe *{EscapeMarkdown(request.RecipeName)}* not found\\.",
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: ct);
            return;
        }

        // Build the formatted message
        var message = FormatCostMessage(result);

        // Build the inline keyboard
        var keyboard = new InlineKeyboardMarkup(
        [
            // Row 1: recalculate button
            [
                InlineKeyboardButton.WithCallbackData(
                    "🔄 Recalculate",
                    $"recalculate:{result.RecipeName}")    // ← callback data: "recalculate:Nastar"
            ],
            // Row 2: view all recipes button
            [
                InlineKeyboardButton.WithCallbackData(
                    "📋 All Recipes",
                    "list_recipes")
            ],
        ]);

        await bot.SendMessage(
            request.ChatId,
            message,
            parseMode: ParseMode.MarkdownV2,
            replyMarkup: keyboard,        // ← attach the keyboard to the message
            cancellationToken: ct);
    }

    // ── Formatting helpers ────────────────────────────────────────

    private static string FormatCostMessage(RecipeCostResult result)
    {
        var lines = result.Items.Select(item =>
            $"  • {EscapeMarkdown(item.IngredientName)} " +
            $"\\({item.Quantity:N0}{EscapeMarkdown(item.Unit ?? "")}\\) " +
            $"— {FormatRupiah(item.LineCost)}"
        );

        var marginEmoji = result.MarginPercent >= 30 ? "✅" :
                          result.MarginPercent >= 10 ? "⚠️" : "❌";

        return $"""
            🍪 *{EscapeMarkdown(result.RecipeName)}*

            *Ingredients:*
            {string.Join("\n", lines)}

            ━━━━━━━━━━━━━━━━━━━━
            💰 *Total Cost:* {FormatRupiah(result.TotalCost)}
            💵 *Selling Price:* {FormatRupiah(result.SellingPrice)}
            📈 *Margin:* {result.MarginPercent:N1}% {marginEmoji}
            """;
    }

    private static string FormatRupiah(decimal amount)
        => $"Rp {amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", "\\.")}";

    private static string EscapeMarkdown(string text)
    {
        char[] specialChars = ['.', '!', '(', ')', '[', ']', '{', '}', '>', '#', '+', '-', '=', '|', '~', '`', '\\'];
        return string.Concat(
            text.ToCharArray().Select(c => specialChars.Contains(c) ? $"\\{c}" : c.ToString())
        );
    }
}
```

---

## Step 2 — Handle callback_query (button clicks)

When a user taps the "🔄 Recalculate" button, Telegram sends a `callback_query`. Update `ProcessUpdate.cs` to handle it:

```csharp
// In ProcessUpdateHandler.Handle(), add to the switch:

case UpdateType.CallbackQuery when update.CallbackQuery is not null:
    await HandleCallbackQuery(update.CallbackQuery, cancellationToken);
    break;
```

Add the handler method:

```csharp
private async Task HandleCallbackQuery(CallbackQuery callbackQuery, CancellationToken ct)
{
    var chatId = callbackQuery.Message!.Chat.Id;
    var data   = callbackQuery.Data ?? "";

    // Always acknowledge the callback first — removes the spinner from the button
    await bot.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: ct);
    // ↑ If you don't call this within 10 seconds, Telegram shows an error to the user

    if (data.StartsWith("recalculate:"))
    {
        var recipeName = data["recalculate:".Length..];    // ← slice from after "recalculate:"
        // C# range syntax: [startIndex..] = everything from startIndex onwards
        // Compare Python: data["recalculate:".len():]

        await mediator.Send(new CostCommand(chatId, recipeName), ct);
    }
    else if (data == "list_recipes")
    {
        // TODO in a real app: send a list of available recipes
        await bot.SendMessage(chatId, "Feature coming soon!", cancellationToken: ct);
    }
}
```

---

## Step 3 — Wire up the /cost command

The routing already exists in `ProcessUpdate.cs` from Lesson 11:

```csharp
case "/cost":
    await mediator.Send(new CostCommand(chatId, args), ct);
    break;
```

No additional wiring needed for callback handling — it's in the same `ProcessUpdateHandler`.

---

## ✅ Run it

```powershell
dotnet run
```

Send `/cost Nastar` to your bot → you'll see:
```
🍪 Nastar

Ingredients:
  • Flour (500g) — Rp 12.500
  • Sugar (150g) — Rp 2.400
  • Butter (250g) — Rp 22.500

━━━━━━━━━━━━━━━━━━━
💰 Total Cost: Rp 37.400
💵 Selling Price: Rp 60.000
📈 Margin: 37.7% ✅

[🔄 Recalculate]
[📋 All Recipes]
```

Tap "🔄 Recalculate" — it re-runs the cost calculation with the latest prices.

---

## 🔑 Key Concepts

| Concept | What it is |
|---------|-----------|
| `InlineKeyboardMarkup` | A keyboard of buttons attached to a message |
| `InlineKeyboardButton.WithCallbackData()` | A button that sends callback data when tapped |
| `callback_query` update | Triggered when user taps an inline keyboard button |
| `AnswerCallbackQuery()` | Must be called within 10s to dismiss the button spinner |
| `CallbackQuery.Data` | The string you defined in `WithCallbackData()` — your routing key |
| Range syntax `data[n..]` | C# slice — take everything from index n onwards |
| `ChatAction.Typing` | Shows "typing..." indicator while processing |

---

## Phase 3 Complete — What you built

```
User                Telegram              Your .NET API           PostgreSQL
  │                     │                      │                       │
  │── /cost Nastar ────►│                      │                       │
  │                     │── POST /webhook ────►│                       │
  │                     │                      │── ProcessUpdateCmd    │
  │                     │                      │── CostCommand         │
  │                     │                      │── GetRecipeCostQuery ─►│
  │                     │                      │◄─ RecipeCostResult ───│
  │                     │◄── SendMessage ──────│                       │
  │◄── cost breakdown ──│                      │                       │
  │                     │                      │                       │
  │── [🔄 Recalculate] ►│                      │                       │
  │                     │── POST /webhook ────►│                       │
  │                     │   (callback_query)   │── AnswerCallbackQuery │
  │                     │                      │── CostCommand (again) │
  │◄── updated cost ────│◄─────────────────────│                       │
```

---

## 🎉 Track Complete

You've built a full application from scratch:

| | What you did |
|-|-------------|
| **Phase 1** | Minimal API CRUD, EF Core + PostgreSQL, validation, OpenAPI docs |
| **Phase 2** | Refactored to MediatR, pipeline behaviors auto-validate + log every request |
| **Phase 3** | Telegram bot reuses MediatR handlers — `/ingredients`, `/cost`, inline keyboard |

**The next steps** (in the [advanced lessons track](../../lessons/00-learning-path.md)):
- Add Purchase domain (scan receipts)
- Add Recipe domain (cost calculation)
- OCR service integration (PaddleOCR)
- Containerize with Docker
