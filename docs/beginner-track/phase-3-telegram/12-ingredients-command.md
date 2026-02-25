# Lesson 12: /ingredients Command

## 🎯 What you'll build
An `/ingredients` bot command that queries the database via MediatR and formats the result as a nicely formatted Telegram message — proving the bot reuses the handlers you already wrote.

---

## 🔧 The Key Insight

Look at the existing `GetIngredientsQuery` handler you built in Lesson 07:

```csharp
public class GetIngredientsHandler(NastartDbContext db)
    : IRequestHandler<GetIngredientsQuery, List<Ingredient>>
{
    public async Task<List<Ingredient>> Handle(
        GetIngredientsQuery request,
        CancellationToken ct)
        => await db.Ingredients.ToListAsync(ct);
}
```

The bot command simply sends the same query:

```csharp
var ingredients = await mediator.Send(new GetIngredientsQuery());
```

**The handler doesn't know or care who's calling it.** HTTP endpoint, Telegram bot, a test — they all use the same query, the same handler, the same database call. This is why we refactored to MediatR.

---

## Step 1 — Create the IngredientsCommand

Create `src/Nastart.Api/Features/Bot/IngredientsCommand.cs`:

```csharp
using MediatR;
using Nastart.Api.Features.Ingredients;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Nastart.Api.Features.Bot;

// ── Command ────────────────────────────────────────────────────────

public record IngredientsCommand(long ChatId) : IRequest;

// ── Handler ────────────────────────────────────────────────────────

public class IngredientsCommandHandler(
    ITelegramBotClient bot,
    IMediator mediator)                    // ← IMediator injected — calls other handlers
    : IRequestHandler<IngredientsCommand>
{
    public async Task Handle(IngredientsCommand request, CancellationToken ct)
    {
        // Reuse the exact same query the REST API uses ↓
        var ingredients = await mediator.Send(new GetIngredientsQuery(), ct);

        if (ingredients.Count == 0)
        {
            await bot.SendMessage(
                request.ChatId,
                "No ingredients found\\. Add some via the API first\\!",
                parseMode: ParseMode.MarkdownV2,
                cancellationToken: ct);
            return;
        }

        // Format as a readable Telegram message
        var lines = ingredients.Select(i =>
        {
            // Escape MarkdownV2 special chars in the name
            var name  = EscapeMarkdown(i.Name);
            var unit  = EscapeMarkdown(i.Unit ?? "—");
            var price = FormatRupiah(i.CurrentPricePerUnit);

            return $"• *{name}* ({unit}) — {price}";
        });

        var message = $"*📦 Ingredients*\n\n{string.Join("\n", lines)}";
        // string.Join = Python's "\n".join(lines) or JS's lines.join("\n")

        await bot.SendMessage(
            request.ChatId,
            message,
            parseMode: ParseMode.MarkdownV2,
            cancellationToken: ct);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// MarkdownV2 requires escaping: . ! ( ) [ ] { } > # + - = | ~ ` \
    /// </summary>
    private static string EscapeMarkdown(string text)
    {
        // These chars need a backslash before them in MarkdownV2
        char[] specialChars = ['.', '!', '(', ')', '[', ']', '{', '}', '>', '#', '+', '-', '=', '|', '~', '`', '\\'];

        // string.Join + char array → iterate and escape
        var escaped = text.ToCharArray()
            .Select(c => specialChars.Contains(c) ? $"\\{c}" : c.ToString());

        return string.Concat(escaped);
    }

    /// <summary>
    /// Format a decimal as Indonesian Rupiah: 12000 → Rp 12.000
    /// </summary>
    private static string FormatRupiah(decimal amount)
    {
        // ":N0" = number with group separator, 0 decimal places
        // InvariantCulture uses comma for grouping; we replace with dot for Indonesian convention
        var formatted = amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
            .Replace(",", "\\.");

        return $"Rp {formatted}";
    }
}
```

---

## Step 2 — No changes to Program.cs or endpoints needed

The handler is automatically discovered by:
```csharp
cfg.RegisterServicesFromAssemblyContaining<Program>()
```

And the routing is already in `ProcessUpdate.cs`:
```csharp
case "/ingredients":
    await mediator.Send(new IngredientsCommand(chatId), ct);
    break;
```

---

## ✅ Run it

```powershell
dotnet run
```

Send `/ingredients` to your Telegram bot. You should see the ingredient list, one per line, with prices.

**Trace through what happened:**
1. User sends `/ingredients`
2. Telegram `POST /telegram/webhook` with an Update
3. `HandleUpdate` → `mediator.Send(new ProcessUpdateCommand(update))`  
4. `ProcessUpdateHandler` → parses text → `mediator.Send(new IngredientsCommand(chatId))`
5. `IngredientsCommandHandler` → `mediator.Send(new GetIngredientsQuery())`
6. `GetIngredientsHandler` → `db.Ingredients.ToListAsync()`
7. Results bubble back up → formatted → `bot.SendMessage()`

---

## What the call chain looks like

```
User → Telegram → POST /telegram/webhook
                        │
                        ▼
                  ProcessUpdateHandler
                        │ mediator.Send(IngredientsCommand)
                        ▼
                  IngredientsCommandHandler
                        │ mediator.Send(GetIngredientsQuery)    ← same query the REST API uses
                        ▼
                  GetIngredientsHandler
                        │ db.Ingredients.ToListAsync()
                        ▼
                  PostgreSQL
                        │ ingredients list
                        ▼
                  Format message text
                        │ bot.SendMessage()
                        ▼
                    Telegram sends reply to user
```

---

## 🔑 Key Concepts

| Concept | What it is |
|---------|-----------|
| Query reuse | Bot and REST API use the same `GetIngredientsQuery` — no duplication |
| MarkdownV2 escaping | Special chars must be backslash-escaped — create a helper |
| `string.Join()` | Joins an array of strings with a separator — like Python's `"\n".join()` |
| `string.Concat()` | Concatenates strings/chars — like Python's `''.join()` |
| `Select()` | Transform each item — like Python's `map()` or JS's `.map()` |

---

➡️ Next: [Lesson 13 — /cost command with inline keyboard](13-cost-command.md)
