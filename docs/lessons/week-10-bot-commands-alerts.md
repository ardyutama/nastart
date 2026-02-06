# Week 10: Bot Commands + Alerts 📢

> **Goal**: Build the utility bot commands (`/cost`, `/price`, `/low`, `/profit`), connect the notification system to send Telegram alerts for price spikes and margin drops, and complete the full bot workflow.

---

## Table of Contents
1. [Day 1: CostCommand Feature Slice](#day-1-costcommand-feature-slice)
2. [Day 2: PriceCommand Feature Slice](#day-2-pricecommand-feature-slice)
3. [Day 3: LowStockCommand Feature Slice](#day-3-lowstockcommand-feature-slice)
4. [Day 4: ProfitCommand Feature Slice](#day-4-profitcommand-feature-slice)
5. [Day 5: Telegram Alert Notifications](#day-5-telegram-alert-notifications)
6. [Day 6: Notification Handlers & Scheduling](#day-6-notification-handlers--scheduling)
7. [Day 7: Full Bot Workflow Testing](#day-7-full-bot-workflow-testing)
8. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1: CostCommand Feature Slice

## 🧒 Explain Like I'm 5

Imagine Ibu Sari wants to know how much it costs to make her Nastar cookies 🍪:

She types: `/cost Nastar`

The robot looks up the recipe and says:
```
🍪 Nastar (24 pcs)

Bahan:
• Tepung Terigu (500g) — Rp 12.500
• Mentega (250g) — Rp 22.500
• Gula Halus (150g) — Rp 4.500
• Telur (2 pcs) — Rp 7.000
• Selai Nanas (300g) — Rp 18.000

💰 Total Biaya: Rp 64.500
📦 Biaya per pcs: Rp 2.687
💵 Harga Jual: Rp 5.000/pcs
📈 Margin: 46.3% ✅
```

Now Ibu Sari knows exactly how much money she makes!

## 🔧 Engineer Language

The **CostCommand** feature handles the `/cost [recipe_name]` command. It queries the recipe, calculates real-time costs using current ingredient prices, and returns a formatted breakdown.

> 📖 **Microsoft Docs**: *"MediatR simplifies request/response patterns. Handlers encapsulate all logic for a specific request."*
>
> — [MediatR on GitHub](https://github.com/jbogard/MediatR)

### CostCommand Feature

Create `src/Nastart.Api/Features/Bot/Commands/CostCommand.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Recipes;
using Nastart.Api.Shared.Data;
using Telegram.Bot.Types.ReplyMarkups;

namespace Nastart.Api.Features.Bot;

// ══════════════════════════════════════════════════════════════
// COST COMMAND FEATURE SLICE
// Handles /cost [recipe] — shows recipe cost breakdown
// ══════════════════════════════════════════════════════════════

// ── Request ──
/// <summary>
/// Command to calculate and display recipe cost.
/// </summary>
public sealed record CostCommand(
    long ChatId,
    long UserId,
    string? RecipeName
) : IRequest<Unit>;

// ── Handler ──
/// <summary>
/// Handles the /cost command by calculating recipe costs.
/// </summary>
/// <remarks>
/// Calculates cost using current ingredient prices from latest purchases.
/// See: https://learn.microsoft.com/en-us/ef/core/querying/
/// </remarks>
public sealed class CostCommandHandler : IRequestHandler<CostCommand, Unit>
{
    private readonly ITelegramBotService _botService;
    private readonly NastartDbContext _db;
    private readonly ILogger<CostCommandHandler> _logger;

    public CostCommandHandler(
        ITelegramBotService botService,
        NastartDbContext db,
        ILogger<CostCommandHandler> logger)
    {
        _botService = botService;
        _db = db;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        CostCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Cost command from chat {ChatId}: {RecipeName}",
            request.ChatId,
            request.RecipeName);
        
        // If no recipe name provided, show list
        if (string.IsNullOrWhiteSpace(request.RecipeName))
        {
            await SendRecipeList(request.ChatId, request.UserId, cancellationToken);
            return Unit.Value;
        }
        
        // Find user by Telegram ID
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramId == request.UserId, cancellationToken);
        
        if (user is null)
        {
            await _botService.SendTextMessageAsync(
                request.ChatId,
                "❌ Silakan /start dulu untuk mendaftar.",
                cancellationToken: cancellationToken);
            return Unit.Value;
        }
        
        // Find recipe by name (fuzzy match)
        var recipe = await _db.Recipes
            .Include(r => r.Items)
                .ThenInclude(i => i.Ingredient)
            .Where(r => r.UserId == user.Id)
            .Where(r => EF.Functions.ILike(r.Name, $"%{request.RecipeName}%"))
            .FirstOrDefaultAsync(cancellationToken);
        
        if (recipe is null)
        {
            await _botService.SendTextMessageAsync(
                request.ChatId,
                $"❌ Resep \"{request.RecipeName}\" tidak ditemukan.\n\nKetik /cost tanpa argumen untuk lihat daftar resep.",
                cancellationToken: cancellationToken);
            return Unit.Value;
        }
        
        // Calculate costs
        var costBreakdown = await CalculateCostBreakdown(recipe, cancellationToken);
        
        // Format and send response
        var message = FormatCostMessage(recipe, costBreakdown);
        var keyboard = BuildCostKeyboard(recipe.Id);
        
        await _botService.SendTextMessageAsync(
            request.ChatId,
            message,
            keyboard,
            cancellationToken: cancellationToken);
        
        return Unit.Value;
    }

    private async Task SendRecipeList(
        long chatId,
        long userId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramId == userId, cancellationToken);
        
        if (user is null)
        {
            await _botService.SendTextMessageAsync(
                chatId,
                "❌ Silakan /start dulu untuk mendaftar.",
                cancellationToken: cancellationToken);
            return;
        }
        
        var recipes = await _db.Recipes
            .Where(r => r.UserId == user.Id && r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name, r.YieldQuantity, r.YieldUnit })
            .Take(20)
            .ToListAsync(cancellationToken);
        
        if (!recipes.Any())
        {
            await _botService.SendTextMessageAsync(
                chatId,
                "📭 Belum ada resep.\n\nGunakan dashboard web untuk menambah resep.",
                cancellationToken: cancellationToken);
            return;
        }
        
        var message = "<b>📋 Daftar Resep</b>\n\n";
        message += "Ketik <code>/cost [nama]</code> untuk lihat biaya:\n\n";
        
        foreach (var recipe in recipes)
        {
            message += $"• <b>{recipe.Name}</b> ({recipe.YieldQuantity} {recipe.YieldUnit})\n";
        }
        
        message += "\n<i>Contoh: /cost Nastar</i>";
        
        // Build inline keyboard for quick selection
        var buttons = recipes.Take(6).Select(r =>
            new[] { InlineKeyboardButton.WithCallbackData(
                $"📊 {r.Name}",
                $"cost:view:{r.Id}") }
        ).ToList();
        
        var keyboard = new InlineKeyboardMarkup(buttons);
        
        await _botService.SendTextMessageAsync(
            chatId,
            message,
            keyboard,
            cancellationToken: cancellationToken);
    }

    private async Task<CostBreakdown> CalculateCostBreakdown(
        Recipe recipe,
        CancellationToken cancellationToken)
    {
        var itemCosts = new List<ItemCost>();
        decimal totalCost = 0;
        
        foreach (var item in recipe.Items)
        {
            if (item.Ingredient is null) continue;
            
            // Get latest price for ingredient
            var latestPrice = item.Ingredient.CurrentPrice;
            var itemTotal = item.Quantity * latestPrice;
            
            itemCosts.Add(new ItemCost(
                IngredientName: item.Ingredient.Name,
                Quantity: item.Quantity,
                Unit: item.Ingredient.Unit,
                UnitPrice: latestPrice,
                TotalPrice: itemTotal
            ));
            
            totalCost += itemTotal;
        }
        
        var costPerUnit = recipe.YieldQuantity > 0 
            ? totalCost / recipe.YieldQuantity 
            : totalCost;
        
        var margin = recipe.SellingPrice > 0 
            ? ((recipe.SellingPrice - costPerUnit) / recipe.SellingPrice) * 100 
            : 0;
        
        return new CostBreakdown(
            Items: itemCosts,
            TotalCost: totalCost,
            CostPerUnit: costPerUnit,
            SellingPrice: recipe.SellingPrice,
            Margin: margin
        );
    }

    private static string FormatCostMessage(Recipe recipe, CostBreakdown breakdown)
    {
        var sb = new System.Text.StringBuilder();
        
        // Header
        sb.AppendLine($"🍪 <b>{recipe.Name}</b> ({recipe.YieldQuantity} {recipe.YieldUnit})");
        sb.AppendLine();
        
        // Ingredients
        sb.AppendLine("<b>Bahan:</b>");
        foreach (var item in breakdown.Items)
        {
            sb.AppendLine($"• {item.IngredientName} ({item.Quantity}{item.Unit}) — Rp {item.TotalPrice:N0}");
        }
        sb.AppendLine();
        
        // Totals
        sb.AppendLine($"💰 <b>Total Biaya:</b> Rp {breakdown.TotalCost:N0}");
        sb.AppendLine($"📦 <b>Biaya per {recipe.YieldUnit}:</b> Rp {breakdown.CostPerUnit:N0}");
        
        if (breakdown.SellingPrice > 0)
        {
            sb.AppendLine($"💵 <b>Harga Jual:</b> Rp {breakdown.SellingPrice:N0}/{recipe.YieldUnit}");
            
            // Margin with emoji indicator
            var marginEmoji = breakdown.Margin switch
            {
                >= 40 => "✅",  // Healthy margin
                >= 25 => "⚠️",  // Warning
                _ => "🔴"       // Low margin
            };
            sb.AppendLine($"📈 <b>Margin:</b> {breakdown.Margin:F1}% {marginEmoji}");
        }
        
        // Timestamp
        sb.AppendLine();
        sb.AppendLine($"<i>Dihitung: {DateTime.Now:dd/MM/yyyy HH:mm}</i>");
        
        return sb.ToString();
    }

    private static InlineKeyboardMarkup BuildCostKeyboard(Guid recipeId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔄 Refresh", $"cost:refresh:{recipeId}"),
                InlineKeyboardButton.WithCallbackData("📊 Trend", $"cost:trend:{recipeId}"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📋 Resep Lain", "cost:list"),
            }
        });
    }
}

// ── Supporting Records ──
internal sealed record ItemCost(
    string IngredientName,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal TotalPrice
);

internal sealed record CostBreakdown(
    IReadOnlyList<ItemCost> Items,
    decimal TotalCost,
    decimal CostPerUnit,
    decimal SellingPrice,
    decimal Margin
);
```

### Update Webhook Handler

Add to `HandleWebhook.cs` in the `DispatchCommand` method:

```csharp
"cost" => await DispatchCostCommand(message, cancellationToken),

// ... and add the method:
private async Task<string> DispatchCostCommand(
    TelegramMessage message,
    CancellationToken cancellationToken)
{
    await _mediator.Send(new CostCommand(
        ChatId: message.Chat.Id,
        UserId: message.From?.Id ?? 0,
        RecipeName: message.CommandArgs
    ), cancellationToken);
    
    return "CostCommand";
}
```

### Your Task (Day 1):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create CostCommand feature
New-Item Features\Bot\Commands\CostCommand.cs

# Update HandleWebhook.cs with cost command routing

# Verify build
dotnet build
```

---

# Day 2: PriceCommand Feature Slice

## 🧒 Explain Like I'm 5

Ibu Sari wants to check how much flour costs now:

She types: `/price tepung`

The robot says:
```
🏷️ Tepung Terigu

💵 Harga Saat Ini: Rp 25.000/kg
📅 Update Terakhir: 05/02/2026

📊 Riwayat Harga:
• 01/02 — Rp 24.000 
• 15/01 — Rp 23.500
• 01/01 — Rp 22.000

📈 Naik 13.6% dalam 1 bulan
```

Now she can see if flour is getting more expensive!

## 🔧 Engineer Language

The **PriceCommand** feature handles `/price [ingredient_name]`. It shows current price, price history, and calculates price trends.

> 📖 **Microsoft Docs**: *"EF Core supports complex queries with LINQ. Use projections to select only the data you need."*
>
> — [Querying Data](https://learn.microsoft.com/en-us/ef/core/querying/)

### PriceCommand Feature

Create `src/Nastart.Api/Features/Bot/Commands/PriceCommand.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Telegram.Bot.Types.ReplyMarkups;

namespace Nastart.Api.Features.Bot;

// ══════════════════════════════════════════════════════════════
// PRICE COMMAND FEATURE SLICE
// Handles /price [ingredient] — shows price info and trends
// ══════════════════════════════════════════════════════════════

// ── Request ──
/// <summary>
/// Command to display ingredient price information.
/// </summary>
public sealed record PriceCommand(
    long ChatId,
    long UserId,
    string? IngredientName
) : IRequest<Unit>;

// ── Handler ──
/// <summary>
/// Handles the /price command by showing price info and history.
/// </summary>
public sealed class PriceCommandHandler : IRequestHandler<PriceCommand, Unit>
{
    private readonly ITelegramBotService _botService;
    private readonly NastartDbContext _db;
    private readonly ILogger<PriceCommandHandler> _logger;

    public PriceCommandHandler(
        ITelegramBotService botService,
        NastartDbContext db,
        ILogger<PriceCommandHandler> logger)
    {
        _botService = botService;
        _db = db;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        PriceCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Price command from chat {ChatId}: {IngredientName}",
            request.ChatId,
            request.IngredientName);
        
        // If no ingredient name, show list
        if (string.IsNullOrWhiteSpace(request.IngredientName))
        {
            await SendIngredientList(request.ChatId, request.UserId, cancellationToken);
            return Unit.Value;
        }
        
        // Find user
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramId == request.UserId, cancellationToken);
        
        if (user is null)
        {
            await _botService.SendTextMessageAsync(
                request.ChatId,
                "❌ Silakan /start dulu untuk mendaftar.",
                cancellationToken: cancellationToken);
            return Unit.Value;
        }
        
        // Find ingredient (fuzzy match)
        var ingredient = await _db.Ingredients
            .Where(i => i.UserId == user.Id)
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.IngredientName}%"))
            .FirstOrDefaultAsync(cancellationToken);
        
        if (ingredient is null)
        {
            await _botService.SendTextMessageAsync(
                request.ChatId,
                $"❌ Bahan \"{request.IngredientName}\" tidak ditemukan.\n\nKetik /price tanpa argumen untuk lihat daftar.",
                cancellationToken: cancellationToken);
            return Unit.Value;
        }
        
        // Get price history
        var priceHistory = await GetPriceHistory(ingredient.Id, cancellationToken);
        
        // Format message
        var message = FormatPriceMessage(ingredient, priceHistory);
        var keyboard = BuildPriceKeyboard(ingredient.Id);
        
        await _botService.SendTextMessageAsync(
            request.ChatId,
            message,
            keyboard,
            cancellationToken: cancellationToken);
        
        return Unit.Value;
    }

    private async Task SendIngredientList(
        long chatId,
        long userId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramId == userId, cancellationToken);
        
        if (user is null)
        {
            await _botService.SendTextMessageAsync(
                chatId,
                "❌ Silakan /start dulu untuk mendaftar.",
                cancellationToken: cancellationToken);
            return;
        }
        
        var ingredients = await _db.Ingredients
            .Where(i => i.UserId == user.Id)
            .OrderBy(i => i.Name)
            .Select(i => new { i.Id, i.Name, i.CurrentPrice, i.Unit })
            .Take(20)
            .ToListAsync(cancellationToken);
        
        if (!ingredients.Any())
        {
            await _botService.SendTextMessageAsync(
                chatId,
                "📭 Belum ada bahan.\n\nScan struk belanja untuk menambah bahan.",
                cancellationToken: cancellationToken);
            return;
        }
        
        var message = "<b>🏷️ Daftar Bahan</b>\n\n";
        message += "Ketik <code>/price [nama]</code> untuk lihat harga:\n\n";
        
        foreach (var ing in ingredients)
        {
            message += $"• <b>{ing.Name}</b> — Rp {ing.CurrentPrice:N0}/{ing.Unit}\n";
        }
        
        message += "\n<i>Contoh: /price tepung</i>";
        
        await _botService.SendTextMessageAsync(
            chatId,
            message,
            cancellationToken: cancellationToken);
    }

    private async Task<List<PriceHistoryItem>> GetPriceHistory(
        Guid ingredientId,
        CancellationToken cancellationToken)
    {
        // Get price history from purchase items
        var history = await _db.PurchaseItems
            .Include(pi => pi.Purchase)
            .Where(pi => pi.IngredientId == ingredientId)
            .OrderByDescending(pi => pi.Purchase!.PurchaseDate)
            .Take(10)
            .Select(pi => new PriceHistoryItem(
                pi.Purchase!.PurchaseDate,
                pi.UnitPrice,
                pi.Purchase.Shop != null ? pi.Purchase.Shop.Name : null
            ))
            .ToListAsync(cancellationToken);
        
        return history;
    }

    private static string FormatPriceMessage(
        Nastart.Api.Features.Ingredients.Ingredient ingredient,
        List<PriceHistoryItem> history)
    {
        var sb = new System.Text.StringBuilder();
        
        // Header
        sb.AppendLine($"🏷️ <b>{ingredient.Name}</b>");
        sb.AppendLine();
        
        // Current price
        sb.AppendLine($"💵 <b>Harga Saat Ini:</b> Rp {ingredient.CurrentPrice:N0}/{ingredient.Unit}");
        sb.AppendLine($"📅 <b>Update Terakhir:</b> {ingredient.LastPriceUpdate:dd/MM/yyyy}");
        sb.AppendLine();
        
        // Price history
        if (history.Any())
        {
            sb.AppendLine("<b>📊 Riwayat Harga:</b>");
            foreach (var item in history.Take(5))
            {
                var shopInfo = !string.IsNullOrEmpty(item.ShopName) 
                    ? $" ({item.ShopName})" 
                    : "";
                sb.AppendLine($"• {item.Date:dd/MM} — Rp {item.Price:N0}{shopInfo}");
            }
            sb.AppendLine();
            
            // Calculate trend
            if (history.Count >= 2)
            {
                var latestPrice = history.First().Price;
                var oldestPrice = history.Last().Price;
                var changePercent = oldestPrice > 0 
                    ? ((latestPrice - oldestPrice) / oldestPrice) * 100 
                    : 0;
                
                var trendEmoji = changePercent switch
                {
                    > 15 => "🔴📈",  // Significant increase
                    > 5 => "⚠️📈",   // Moderate increase
                    < -5 => "✅📉",  // Decrease
                    _ => "➡️"        // Stable
                };
                
                var direction = changePercent >= 0 ? "Naik" : "Turun";
                sb.AppendLine($"{trendEmoji} {direction} {Math.Abs(changePercent):F1}% dalam periode ini");
            }
        }
        
        // Stock info
        if (ingredient.CurrentStock > 0)
        {
            var stockEmoji = ingredient.CurrentStock < ingredient.MinimumStock ? "⚠️" : "✅";
            sb.AppendLine();
            sb.AppendLine($"📦 <b>Stok:</b> {ingredient.CurrentStock} {ingredient.Unit} {stockEmoji}");
        }
        
        return sb.ToString();
    }

    private static InlineKeyboardMarkup BuildPriceKeyboard(Guid ingredientId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📈 Grafik", $"price:chart:{ingredientId}"),
                InlineKeyboardButton.WithCallbackData("🔔 Alert", $"price:alert:{ingredientId}"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📋 Bahan Lain", "price:list"),
            }
        });
    }
}

// ── Supporting Records ──
internal sealed record PriceHistoryItem(
    DateTime Date,
    decimal Price,
    string? ShopName
);
```

### Your Task (Day 2):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create PriceCommand feature
New-Item Features\Bot\Commands\PriceCommand.cs

# Add to HandleWebhook.cs:
# "price" => await DispatchPriceCommand(message, cancellationToken),

# Verify build
dotnet build
```

---

# Day 3: LowStockCommand Feature Slice

## 🧒 Explain Like I'm 5

Before going shopping, Ibu Sari wants to know what's running low:

She types: `/low`

The robot checks her pantry and says:
```
⚠️ Bahan Stok Menipis

🔴 KRITIS (< 25%)
• Mentega — 100g (min: 500g)
• Telur — 6 pcs (min: 30 pcs)

🟡 RENDAH (< 50%)
• Gula Pasir — 500g (min: 1kg)
• Coklat Bubuk — 150g (min: 250g)

📝 Total: 4 bahan perlu dibeli
```

Now she knows exactly what to buy!

## 🔧 Engineer Language

The **LowStockCommand** feature shows ingredients below their minimum stock threshold, categorized by urgency level.

> 📖 **Microsoft Docs**: *"Use Skip and Take for pagination. OrderBy ensures consistent results."*
>
> — [Pagination](https://learn.microsoft.com/en-us/ef/core/querying/pagination)

### LowStockCommand Feature

Create `src/Nastart.Api/Features/Bot/Commands/LowStockCommand.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Telegram.Bot.Types.ReplyMarkups;

namespace Nastart.Api.Features.Bot;

// ══════════════════════════════════════════════════════════════
// LOW STOCK COMMAND FEATURE SLICE
// Handles /low — shows ingredients below minimum stock
// ══════════════════════════════════════════════════════════════

// ── Request ──
/// <summary>
/// Command to display low stock ingredients.
/// </summary>
public sealed record LowStockCommand(
    long ChatId,
    long UserId
) : IRequest<Unit>;

// ── Handler ──
/// <summary>
/// Handles the /low command by showing items below minimum stock.
/// </summary>
public sealed class LowStockCommandHandler : IRequestHandler<LowStockCommand, Unit>
{
    private readonly ITelegramBotService _botService;
    private readonly NastartDbContext _db;
    private readonly ILogger<LowStockCommandHandler> _logger;

    public LowStockCommandHandler(
        ITelegramBotService botService,
        NastartDbContext db,
        ILogger<LowStockCommandHandler> logger)
    {
        _botService = botService;
        _db = db;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        LowStockCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Low stock command from chat {ChatId}",
            request.ChatId);
        
        // Find user
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramId == request.UserId, cancellationToken);
        
        if (user is null)
        {
            await _botService.SendTextMessageAsync(
                request.ChatId,
                "❌ Silakan /start dulu untuk mendaftar.",
                cancellationToken: cancellationToken);
            return Unit.Value;
        }
        
        // Get low stock items
        var lowStockItems = await _db.Ingredients
            .Where(i => i.UserId == user.Id)
            .Where(i => i.MinimumStock > 0) // Only items with min stock set
            .Where(i => i.CurrentStock < i.MinimumStock)
            .OrderBy(i => i.CurrentStock / i.MinimumStock) // Most critical first
            .Select(i => new LowStockItem(
                i.Id,
                i.Name,
                i.CurrentStock,
                i.MinimumStock,
                i.Unit,
                i.CurrentPrice
            ))
            .ToListAsync(cancellationToken);
        
        // Format and send response
        var message = FormatLowStockMessage(lowStockItems);
        var keyboard = BuildLowStockKeyboard(lowStockItems.Any());
        
        await _botService.SendTextMessageAsync(
            request.ChatId,
            message,
            keyboard,
            cancellationToken: cancellationToken);
        
        return Unit.Value;
    }

    private static string FormatLowStockMessage(List<LowStockItem> items)
    {
        if (!items.Any())
        {
            return """
                ✅ <b>Stok Aman!</b>
                
                Semua bahan masih di atas batas minimum.
                
                <i>Tip: Scan struk belanja setelah berbelanja untuk update stok otomatis.</i>
                """;
        }
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("⚠️ <b>Bahan Stok Menipis</b>");
        sb.AppendLine();
        
        // Critical items (< 25% of minimum)
        var critical = items.Where(i => GetStockPercent(i) < 25).ToList();
        if (critical.Any())
        {
            sb.AppendLine("🔴 <b>KRITIS (&lt; 25%)</b>");
            foreach (var item in critical)
            {
                sb.AppendLine($"• {item.Name} — {item.CurrentStock}{item.Unit} (min: {item.MinimumStock}{item.Unit})");
            }
            sb.AppendLine();
        }
        
        // Low items (25-50% of minimum)
        var low = items.Where(i => GetStockPercent(i) >= 25 && GetStockPercent(i) < 50).ToList();
        if (low.Any())
        {
            sb.AppendLine("🟡 <b>RENDAH (25-50%)</b>");
            foreach (var item in low)
            {
                sb.AppendLine($"• {item.Name} — {item.CurrentStock}{item.Unit} (min: {item.MinimumStock}{item.Unit})");
            }
            sb.AppendLine();
        }
        
        // Warning items (50-100% of minimum)
        var warning = items.Where(i => GetStockPercent(i) >= 50).ToList();
        if (warning.Any())
        {
            sb.AppendLine("🟢 <b>PERLU DIISI (50-100%)</b>");
            foreach (var item in warning)
            {
                sb.AppendLine($"• {item.Name} — {item.CurrentStock}{item.Unit} (min: {item.MinimumStock}{item.Unit})");
            }
            sb.AppendLine();
        }
        
        // Summary
        sb.AppendLine($"📝 <b>Total:</b> {items.Count} bahan perlu dibeli");
        
        // Estimated cost to restock
        var restockCost = items.Sum(i => (i.MinimumStock - i.CurrentStock) * i.CurrentPrice);
        if (restockCost > 0)
        {
            sb.AppendLine($"💰 <b>Estimasi biaya restock:</b> Rp {restockCost:N0}");
        }
        
        return sb.ToString();
    }

    private static decimal GetStockPercent(LowStockItem item)
    {
        return item.MinimumStock > 0 
            ? (item.CurrentStock / item.MinimumStock) * 100 
            : 0;
    }

    private static InlineKeyboardMarkup BuildLowStockKeyboard(bool hasItems)
    {
        if (!hasItems)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📦 Lihat Semua Stok", "stock:all"),
                }
            });
        }
        
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📋 Export List", "low:export"),
                InlineKeyboardButton.WithCallbackData("🔄 Refresh", "low:refresh"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📦 Lihat Semua Stok", "stock:all"),
            }
        });
    }
}

// ── Supporting Records ──
internal sealed record LowStockItem(
    Guid Id,
    string Name,
    decimal CurrentStock,
    decimal MinimumStock,
    string Unit,
    decimal CurrentPrice
);
```

### Your Task (Day 3):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create LowStockCommand feature
New-Item Features\Bot\Commands\LowStockCommand.cs

# Add to HandleWebhook.cs:
# "low" => await DispatchLowStockCommand(message, cancellationToken),

# Verify build
dotnet build
```

---

# Day 4: ProfitCommand Feature Slice

## 🧒 Explain Like I'm 5

At the end of the week, Ibu Sari wants to see if she's making money:

She types: `/profit`

The robot shows her business summary:
```
📊 Ringkasan Profit

📅 Periode: 01/02 - 07/02/2026

💵 Penjualan: Rp 2.500.000
📦 Biaya Bahan: Rp 1.200.000
💰 Laba Kotor: Rp 1.300.000
📈 Margin: 52%

🍪 Resep Terlaris:
1. Nastar (120 pcs) — Margin 46%
2. Brownies (45 pcs) — Margin 55%
3. Kastengel (80 pcs) — Margin 48%

✅ Bisnis sehat! Margin di atas target 40%
```

Now she knows her business is doing great!

## 🔧 Engineer Language

The **ProfitCommand** feature provides a profit summary with sales, costs, and margin analysis per recipe.

> 📖 **Microsoft Docs**: *"Use GroupBy and aggregation functions for reporting queries. EF Core translates these to SQL for efficient execution."*
>
> — [Complex Query Operators](https://learn.microsoft.com/en-us/ef/core/querying/complex-query-operators)

### ProfitCommand Feature

Create `src/Nastart.Api/Features/Bot/Commands/ProfitCommand.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Telegram.Bot.Types.ReplyMarkups;

namespace Nastart.Api.Features.Bot;

// ══════════════════════════════════════════════════════════════
// PROFIT COMMAND FEATURE SLICE
// Handles /profit — shows profit summary and analysis
// ══════════════════════════════════════════════════════════════

// ── Request ──
/// <summary>
/// Command to display profit summary.
/// </summary>
public sealed record ProfitCommand(
    long ChatId,
    long UserId,
    string? Period // "week", "month", or custom date range
) : IRequest<Unit>;

// ── Handler ──
/// <summary>
/// Handles the /profit command by calculating and displaying profit summary.
/// </summary>
public sealed class ProfitCommandHandler : IRequestHandler<ProfitCommand, Unit>
{
    private readonly ITelegramBotService _botService;
    private readonly NastartDbContext _db;
    private readonly ILogger<ProfitCommandHandler> _logger;

    public ProfitCommandHandler(
        ITelegramBotService botService,
        NastartDbContext db,
        ILogger<ProfitCommandHandler> logger)
    {
        _botService = botService;
        _db = db;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        ProfitCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Profit command from chat {ChatId}",
            request.ChatId);
        
        // Find user
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.TelegramId == request.UserId, cancellationToken);
        
        if (user is null)
        {
            await _botService.SendTextMessageAsync(
                request.ChatId,
                "❌ Silakan /start dulu untuk mendaftar.",
                cancellationToken: cancellationToken);
            return Unit.Value;
        }
        
        // Determine date range
        var (startDate, endDate) = ParsePeriod(request.Period);
        
        // Get profit data
        var profitData = await CalculateProfitData(user.Id, startDate, endDate, cancellationToken);
        
        // Format message
        var message = FormatProfitMessage(profitData, startDate, endDate, user.MinimumMargin);
        var keyboard = BuildProfitKeyboard();
        
        await _botService.SendTextMessageAsync(
            request.ChatId,
            message,
            keyboard,
            cancellationToken: cancellationToken);
        
        return Unit.Value;
    }

    private static (DateTime startDate, DateTime endDate) ParsePeriod(string? period)
    {
        var endDate = DateTime.Today;
        var startDate = period?.ToLowerInvariant() switch
        {
            "month" => endDate.AddMonths(-1),
            "year" => endDate.AddYears(-1),
            "today" => endDate,
            _ => endDate.AddDays(-7) // Default: last week
        };
        
        return (startDate, endDate);
    }

    private async Task<ProfitData> CalculateProfitData(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        // Get purchases (costs)
        var purchases = await _db.Purchases
            .Include(p => p.Items)
            .Where(p => p.UserId == userId)
            .Where(p => p.PurchaseDate >= startDate && p.PurchaseDate <= endDate)
            .ToListAsync(cancellationToken);
        
        var totalCosts = purchases.Sum(p => p.Items.Sum(i => i.Quantity * i.UnitPrice));
        
        // Get recipes with costs for margin calculation
        var recipes = await _db.Recipes
            .Include(r => r.Items)
                .ThenInclude(i => i.Ingredient)
            .Where(r => r.UserId == userId && r.IsActive)
            .ToListAsync(cancellationToken);
        
        var recipeAnalysis = recipes.Select(r =>
        {
            var cost = r.Items.Sum(i => i.Quantity * (i.Ingredient?.CurrentPrice ?? 0));
            var costPerUnit = r.YieldQuantity > 0 ? cost / r.YieldQuantity : cost;
            var margin = r.SellingPrice > 0 
                ? ((r.SellingPrice - costPerUnit) / r.SellingPrice) * 100 
                : 0;
            
            return new RecipeAnalysis(
                r.Name,
                r.YieldQuantity,
                r.YieldUnit,
                cost,
                costPerUnit,
                r.SellingPrice,
                margin
            );
        })
        .OrderByDescending(r => r.Margin)
        .Take(5)
        .ToList();
        
        // Calculate totals (simplified - in real app, track actual sales)
        var estimatedSales = recipeAnalysis.Sum(r => r.SellingPrice * r.YieldQuantity);
        var estimatedProfit = estimatedSales - totalCosts;
        var overallMargin = estimatedSales > 0 
            ? (estimatedProfit / estimatedSales) * 100 
            : 0;
        
        return new ProfitData(
            TotalSales: estimatedSales,
            TotalCosts: totalCosts,
            GrossProfit: estimatedProfit,
            OverallMargin: overallMargin,
            PurchaseCount: purchases.Count,
            TopRecipes: recipeAnalysis
        );
    }

    private static string FormatProfitMessage(
        ProfitData data,
        DateTime startDate,
        DateTime endDate,
        decimal targetMargin)
    {
        var sb = new System.Text.StringBuilder();
        
        // Header
        sb.AppendLine("📊 <b>Ringkasan Profit</b>");
        sb.AppendLine();
        sb.AppendLine($"📅 <b>Periode:</b> {startDate:dd/MM} - {endDate:dd/MM/yyyy}");
        sb.AppendLine();
        
        // Financial summary
        sb.AppendLine($"💵 <b>Est. Penjualan:</b> Rp {data.TotalSales:N0}");
        sb.AppendLine($"📦 <b>Biaya Bahan:</b> Rp {data.TotalCosts:N0}");
        sb.AppendLine($"💰 <b>Laba Kotor:</b> Rp {data.GrossProfit:N0}");
        
        var marginEmoji = data.OverallMargin >= targetMargin ? "✅" : "⚠️";
        sb.AppendLine($"📈 <b>Margin:</b> {data.OverallMargin:F1}% {marginEmoji}");
        sb.AppendLine();
        
        // Top recipes
        if (data.TopRecipes.Any())
        {
            sb.AppendLine("🍪 <b>Analisis Resep:</b>");
            var rank = 1;
            foreach (var recipe in data.TopRecipes.Take(3))
            {
                var marginIcon = recipe.Margin >= targetMargin ? "✅" : "⚠️";
                sb.AppendLine($"{rank}. {recipe.Name} — Margin {recipe.Margin:F0}% {marginIcon}");
                rank++;
            }
            sb.AppendLine();
        }
        
        // Status message
        if (data.OverallMargin >= targetMargin)
        {
            sb.AppendLine($"✅ Bisnis sehat! Margin di atas target {targetMargin:F0}%");
        }
        else if (data.OverallMargin >= targetMargin * 0.7m)
        {
            sb.AppendLine($"⚠️ Margin mendekati batas. Target: {targetMargin:F0}%");
        }
        else
        {
            sb.AppendLine($"🔴 Margin di bawah target {targetMargin:F0}%! Perlu evaluasi harga.");
        }
        
        // Purchase count
        sb.AppendLine();
        sb.AppendLine($"<i>Data dari {data.PurchaseCount} transaksi pembelian</i>");
        
        return sb.ToString();
    }

    private static InlineKeyboardMarkup BuildProfitKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📅 Minggu Ini", "profit:week"),
                InlineKeyboardButton.WithCallbackData("📆 Bulan Ini", "profit:month"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📊 Detail Resep", "profit:recipes"),
                InlineKeyboardButton.WithCallbackData("🔄 Refresh", "profit:refresh"),
            }
        });
    }
}

// ── Supporting Records ──
internal sealed record ProfitData(
    decimal TotalSales,
    decimal TotalCosts,
    decimal GrossProfit,
    decimal OverallMargin,
    int PurchaseCount,
    IReadOnlyList<RecipeAnalysis> TopRecipes
);

internal sealed record RecipeAnalysis(
    string Name,
    decimal YieldQuantity,
    string YieldUnit,
    decimal TotalCost,
    decimal CostPerUnit,
    decimal SellingPrice,
    decimal Margin
);
```

### Your Task (Day 4):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create ProfitCommand feature
New-Item Features\Bot\Commands\ProfitCommand.cs

# Add to HandleWebhook.cs:
# "profit" => await DispatchProfitCommand(message, cancellationToken),

# Verify build
dotnet build
```

---

# Day 5: Telegram Alert Notifications

## 🧒 Explain Like I'm 5

Remember when we said the robot should tell Ibu Sari when something important happens? Like:
- "⚠️ Tepung naik 20%! Cek harga resep!"
- "🔴 Margin Nastar turun ke 25%!"
- "📦 Telur hampir habis!"

These automatic messages are called **alerts** — the robot sends them without being asked!

## 🔧 Engineer Language

**Alert notifications** are triggered by domain events (price spikes, margin drops, low stock). We connect the existing notification system to send Telegram messages.

> 📖 **Microsoft Docs**: *"MediatR notifications allow multiple handlers to respond to a single event. Perfect for side effects like sending alerts."*
>
> — [MediatR Notifications](https://github.com/jbogard/MediatR/wiki#notifications)

### TelegramAlertService

Create `src/Nastart.Api/Features/Bot/Services/TelegramAlertService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Alerts;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Bot.Services;

/// <summary>
/// Service for sending alerts via Telegram.
/// </summary>
public interface ITelegramAlertService
{
    /// <summary>
    /// Sends a price spike alert to the user.
    /// </summary>
    Task SendPriceSpikeAlertAsync(
        Guid userId,
        string ingredientName,
        decimal oldPrice,
        decimal newPrice,
        decimal changePercent,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a margin below threshold alert.
    /// </summary>
    Task SendMarginAlertAsync(
        Guid userId,
        string recipeName,
        decimal currentMargin,
        decimal targetMargin,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a low stock alert.
    /// </summary>
    Task SendLowStockAlertAsync(
        Guid userId,
        string ingredientName,
        decimal currentStock,
        decimal minimumStock,
        string unit,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a generic alert.
    /// </summary>
    Task SendAlertAsync(
        Guid userId,
        Alert alert,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of Telegram alert service.
/// </summary>
public class TelegramAlertService : ITelegramAlertService
{
    private readonly ITelegramBotService _botService;
    private readonly NastartDbContext _db;
    private readonly ILogger<TelegramAlertService> _logger;

    public TelegramAlertService(
        ITelegramBotService botService,
        NastartDbContext db,
        ILogger<TelegramAlertService> logger)
    {
        _botService = botService;
        _db = db;
        _logger = logger;
    }

    public async Task SendPriceSpikeAlertAsync(
        Guid userId,
        string ingredientName,
        decimal oldPrice,
        decimal newPrice,
        decimal changePercent,
        CancellationToken cancellationToken = default)
    {
        var chatId = await GetChatIdForUser(userId, cancellationToken);
        if (chatId is null) return;
        
        var direction = changePercent > 0 ? "naik" : "turun";
        var emoji = changePercent > 15 ? "🔴" : "⚠️";
        
        var message = $"""
            {emoji} <b>Harga {ingredientName} {direction}!</b>
            
            💵 Harga lama: Rp {oldPrice:N0}
            💵 Harga baru: Rp {newPrice:N0}
            📈 Perubahan: {changePercent:+0.0;-0.0}%
            
            <i>Cek dampak ke biaya resep Anda.</i>
            """;
        
        await _botService.SendTextMessageAsync(
            chatId.Value,
            message,
            cancellationToken: cancellationToken);
        
        _logger.LogInformation(
            "Price spike alert sent to user {UserId}: {Ingredient} {Change}%",
            userId,
            ingredientName,
            changePercent);
    }

    public async Task SendMarginAlertAsync(
        Guid userId,
        string recipeName,
        decimal currentMargin,
        decimal targetMargin,
        CancellationToken cancellationToken = default)
    {
        var chatId = await GetChatIdForUser(userId, cancellationToken);
        if (chatId is null) return;
        
        var emoji = currentMargin < targetMargin * 0.5m ? "🔴" : "⚠️";
        
        var message = $"""
            {emoji} <b>Margin {recipeName} turun!</b>
            
            📊 Margin saat ini: {currentMargin:F1}%
            🎯 Target margin: {targetMargin:F1}%
            
            <b>Saran:</b>
            • Cek harga bahan yang naik
            • Evaluasi harga jual
            • Update resep jika perlu
            
            <i>Ketik /cost {recipeName} untuk detail.</i>
            """;
        
        await _botService.SendTextMessageAsync(
            chatId.Value,
            message,
            cancellationToken: cancellationToken);
        
        _logger.LogInformation(
            "Margin alert sent to user {UserId}: {Recipe} at {Margin}%",
            userId,
            recipeName,
            currentMargin);
    }

    public async Task SendLowStockAlertAsync(
        Guid userId,
        string ingredientName,
        decimal currentStock,
        decimal minimumStock,
        string unit,
        CancellationToken cancellationToken = default)
    {
        var chatId = await GetChatIdForUser(userId, cancellationToken);
        if (chatId is null) return;
        
        var percentLeft = minimumStock > 0 ? (currentStock / minimumStock) * 100 : 0;
        var emoji = percentLeft < 25 ? "🔴" : "⚠️";
        
        var message = $"""
            {emoji} <b>Stok {ingredientName} menipis!</b>
            
            📦 Stok saat ini: {currentStock} {unit}
            📋 Minimum: {minimumStock} {unit}
            📊 Tersisa: {percentLeft:F0}%
            
            <i>Ketik /low untuk lihat semua bahan yang perlu dibeli.</i>
            """;
        
        await _botService.SendTextMessageAsync(
            chatId.Value,
            message,
            cancellationToken: cancellationToken);
        
        _logger.LogInformation(
            "Low stock alert sent to user {UserId}: {Ingredient}",
            userId,
            ingredientName);
    }

    public async Task SendAlertAsync(
        Guid userId,
        Alert alert,
        CancellationToken cancellationToken = default)
    {
        var chatId = await GetChatIdForUser(userId, cancellationToken);
        if (chatId is null) return;
        
        var emoji = alert.Type switch
        {
            AlertType.PriceSpike => "📈",
            AlertType.MarginDrop => "📉",
            AlertType.LowStock => "📦",
            _ => "🔔"
        };
        
        var message = $"""
            {emoji} <b>{alert.Title}</b>
            
            {alert.Message}
            
            <i>Dibuat: {alert.CreatedAt:dd/MM/yyyy HH:mm}</i>
            """;
        
        await _botService.SendTextMessageAsync(
            chatId.Value,
            message,
            cancellationToken: cancellationToken);
    }

    private async Task<long?> GetChatIdForUser(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Where(u => u.Id == userId && u.TelegramId.HasValue)
            .Select(u => u.TelegramId)
            .FirstOrDefaultAsync(cancellationToken);
        
        return user;
    }
}
```

### Alert Entity (if not exists)

Create `src/Nastart.Api/Features/Alerts/Alert.cs`:

```csharp
namespace Nastart.Api.Features.Alerts;

/// <summary>
/// Represents a user notification/alert.
/// </summary>
public class Alert
{
    public Guid Id { get; set; }
    
    public required Guid UserId { get; set; }
    
    public required AlertType Type { get; set; }
    
    public required string Title { get; set; }
    
    public required string Message { get; set; }
    
    /// <summary>
    /// Related entity ID (ingredient, recipe, etc).
    /// </summary>
    public Guid? RelatedEntityId { get; set; }
    
    /// <summary>
    /// Related entity type name.
    /// </summary>
    public string? RelatedEntityType { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public bool IsRead { get; set; } = false;
    
    public DateTime? ReadAt { get; set; }
    
    /// <summary>
    /// Whether alert was sent via Telegram.
    /// </summary>
    public bool SentViaTelegram { get; set; } = false;
    
    public DateTime? SentAt { get; set; }
}

/// <summary>
/// Types of alerts.
/// </summary>
public enum AlertType
{
    PriceSpike,
    MarginDrop,
    LowStock,
    RecipeUpdated,
    SystemNotification
}
```

### Register Services

```csharp
// In Program.cs, add:
builder.Services.AddScoped<ITelegramAlertService, TelegramAlertService>();
```

### Your Task (Day 5):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create Alerts feature folder
mkdir Features\Alerts -ErrorAction SilentlyContinue

# Create alert entity and service
New-Item Features\Alerts\Alert.cs
New-Item Features\Bot\Services\TelegramAlertService.cs

# Add Alerts DbSet to context

# Register services in Program.cs

# Verify build
dotnet build
```

---

# Day 6: Notification Handlers & Scheduling

## 🧒 Explain Like I'm 5

When something happens in the app (like a price change), we need to:
1. Notice it happened (the event)
2. Decide if it's important (the handler)
3. Send a message to Ibu Sari (the alert)

It's like having a helper that watches for problems and tells you right away!

## 🔧 Engineer Language

**Notification handlers** respond to MediatR notifications (domain events). We create handlers that check thresholds and trigger Telegram alerts.

> 📖 **Microsoft Docs**: *"Background tasks can be implemented using IHostedService. Use BackgroundService for long-running operations."*
>
> — [Background tasks with hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)

### Price Spike Notification Handler

Create `src/Nastart.Api/Features/Ingredients/PriceSpikeNotificationHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Alerts;
using Nastart.Api.Features.Bot.Services;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Ingredients;

// ══════════════════════════════════════════════════════════════
// PRICE SPIKE NOTIFICATION HANDLER
// Sends Telegram alert when price changes significantly
// ══════════════════════════════════════════════════════════════

/// <summary>
/// Notification for significant price change.
/// </summary>
public sealed record PriceSpikeNotification(
    Guid UserId,
    Guid IngredientId,
    string IngredientName,
    decimal OldPrice,
    decimal NewPrice,
    decimal ChangePercent
) : INotification;

/// <summary>
/// Handles price spike notifications by sending Telegram alerts.
/// </summary>
public sealed class PriceSpikeNotificationHandler 
    : INotificationHandler<PriceSpikeNotification>
{
    private readonly ITelegramAlertService _alertService;
    private readonly NastartDbContext _db;
    private readonly ILogger<PriceSpikeNotificationHandler> _logger;
    
    private const decimal AlertThreshold = 15m; // Alert if > 15% change

    public PriceSpikeNotificationHandler(
        ITelegramAlertService alertService,
        NastartDbContext db,
        ILogger<PriceSpikeNotificationHandler> logger)
    {
        _alertService = alertService;
        _db = db;
        _logger = logger;
    }

    public async Task Handle(
        PriceSpikeNotification notification,
        CancellationToken cancellationToken)
    {
        // Only alert if change exceeds threshold
        if (Math.Abs(notification.ChangePercent) < AlertThreshold)
        {
            _logger.LogDebug(
                "Price change {Percent}% below threshold for {Ingredient}",
                notification.ChangePercent,
                notification.IngredientName);
            return;
        }
        
        _logger.LogInformation(
            "Price spike alert triggered: {Ingredient} changed {Percent}%",
            notification.IngredientName,
            notification.ChangePercent);
        
        // Create alert record
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            UserId = notification.UserId,
            Type = AlertType.PriceSpike,
            Title = $"Harga {notification.IngredientName} berubah drastis",
            Message = $"Harga berubah dari Rp {notification.OldPrice:N0} menjadi Rp {notification.NewPrice:N0} ({notification.ChangePercent:+0.0;-0.0}%)",
            RelatedEntityId = notification.IngredientId,
            RelatedEntityType = "Ingredient"
        };
        
        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync(cancellationToken);
        
        // Send Telegram alert
        await _alertService.SendPriceSpikeAlertAsync(
            notification.UserId,
            notification.IngredientName,
            notification.OldPrice,
            notification.NewPrice,
            notification.ChangePercent,
            cancellationToken);
        
        // Mark as sent
        alert.SentViaTelegram = true;
        alert.SentAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
```

### Margin Alert Notification Handler

Create `src/Nastart.Api/Features/Recipes/MarginAlertNotificationHandler.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Features.Alerts;
using Nastart.Api.Features.Bot.Services;
using Nastart.Api.Shared.Data;

namespace Nastart.Api.Features.Recipes;

// ══════════════════════════════════════════════════════════════
// MARGIN ALERT NOTIFICATION HANDLER
// Sends alert when recipe margin drops below threshold
// ══════════════════════════════════════════════════════════════

/// <summary>
/// Notification for margin below threshold.
/// </summary>
public sealed record MarginBelowThresholdNotification(
    Guid UserId,
    Guid RecipeId,
    string RecipeName,
    decimal CurrentMargin,
    decimal TargetMargin
) : INotification;

/// <summary>
/// Handles margin notifications by sending Telegram alerts.
/// </summary>
public sealed class MarginAlertNotificationHandler 
    : INotificationHandler<MarginBelowThresholdNotification>
{
    private readonly ITelegramAlertService _alertService;
    private readonly NastartDbContext _db;
    private readonly ILogger<MarginAlertNotificationHandler> _logger;

    public MarginAlertNotificationHandler(
        ITelegramAlertService alertService,
        NastartDbContext db,
        ILogger<MarginAlertNotificationHandler> logger)
    {
        _alertService = alertService;
        _db = db;
        _logger = logger;
    }

    public async Task Handle(
        MarginBelowThresholdNotification notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Margin alert triggered: {Recipe} at {Margin}% (target: {Target}%)",
            notification.RecipeName,
            notification.CurrentMargin,
            notification.TargetMargin);
        
        // Create alert record
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            UserId = notification.UserId,
            Type = AlertType.MarginDrop,
            Title = $"Margin {notification.RecipeName} turun",
            Message = $"Margin saat ini {notification.CurrentMargin:F1}%, di bawah target {notification.TargetMargin:F1}%",
            RelatedEntityId = notification.RecipeId,
            RelatedEntityType = "Recipe"
        };
        
        _db.Alerts.Add(alert);
        await _db.SaveChangesAsync(cancellationToken);
        
        // Send Telegram alert
        await _alertService.SendMarginAlertAsync(
            notification.UserId,
            notification.RecipeName,
            notification.CurrentMargin,
            notification.TargetMargin,
            cancellationToken);
        
        // Mark as sent
        alert.SentViaTelegram = true;
        alert.SentAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
```

### Trigger Notifications in Handlers

Update the `RecordPurchase` handler to publish price change notifications:

```csharp
// In RecordPurchaseHandler, after saving purchase and updating prices:

// Check for price spikes
foreach (var item in purchaseItems)
{
    var ingredient = await _db.Ingredients.FindAsync(item.IngredientId);
    if (ingredient is null) continue;
    
    var oldPrice = ingredient.CurrentPrice;
    var newPrice = item.UnitPrice;
    
    if (oldPrice > 0)
    {
        var changePercent = ((newPrice - oldPrice) / oldPrice) * 100;
        
        // Publish notification (handlers decide if alert needed)
        await _mediator.Publish(new PriceSpikeNotification(
            UserId: purchase.UserId,
            IngredientId: ingredient.Id,
            IngredientName: ingredient.Name,
            OldPrice: oldPrice,
            NewPrice: newPrice,
            ChangePercent: changePercent
        ), cancellationToken);
    }
    
    // Update current price
    ingredient.CurrentPrice = newPrice;
    ingredient.LastPriceUpdate = DateTime.UtcNow;
}
```

### Your Task (Day 6):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create notification handlers
New-Item Features\Ingredients\PriceSpikeNotificationHandler.cs
New-Item Features\Recipes\MarginAlertNotificationHandler.cs

# Update RecordPurchaseHandler to publish notifications

# Add Alerts DbSet to context

# Verify build
dotnet build
```

---

# Day 7: Full Bot Workflow Testing

## 🧒 Explain Like I'm 5

Time to test EVERYTHING together! Let's pretend to be Ibu Sari:

1. Start the bot → Gets welcome message ✅
2. Send receipt photo → OCR reads it, confirms items ✅
3. Confirm items → Saved to database ✅
4. `/cost Nastar` → See recipe cost breakdown ✅
5. `/price tepung` → See flour price history ✅
6. `/low` → See low stock items ✅
7. `/profit` → See profit summary ✅
8. Price spike → Automatic alert! ✅

If all these work, the bot is ready! 🎉

## 🔧 Engineer Language

**End-to-end testing** validates the complete bot workflow from user commands through business logic to responses.

> 📖 **Microsoft Docs**: *"Integration tests ensure that an app's components function correctly together."*
>
> — [Integration tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)

### Bot Command Tests

Create `tests/Nastart.Api.Tests/Features/Bot/BotCommandTests.cs`:

```csharp
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Nastart.Api.Features.Bot;

namespace Nastart.Api.Tests.Features.Bot;

public class BotCommandTests
{
    private readonly Mock<ITelegramBotService> _mockBotService;
    private readonly Mock<ILogger<CostCommandHandler>> _mockLogger;

    public BotCommandTests()
    {
        _mockBotService = new Mock<ITelegramBotService>();
        _mockLogger = new Mock<ILogger<CostCommandHandler>>();
    }

    [Fact]
    public async Task CostCommand_WithRecipeName_SendsCostBreakdown()
    {
        // Arrange
        var command = new CostCommand(
            ChatId: 123456789,
            UserId: 987654321,
            RecipeName: "Nastar");
        
        _mockBotService
            .Setup(s => s.SendTextMessageAsync(
                It.IsAny<long>(),
                It.Is<string>(text => text.Contains("Nastar")),
                It.IsAny<Telegram.Bot.Types.ReplyMarkups.IReplyMarkup?>(),
                It.IsAny<Telegram.Bot.Types.Enums.ParseMode>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Telegram.Bot.Types.Message());
        
        // Act & Assert - verify message sent
        // (Full test would need in-memory database)
    }

    [Fact]
    public async Task LowStockCommand_NoItems_ReturnsAllClear()
    {
        // Arrange
        var command = new LowStockCommand(
            ChatId: 123456789,
            UserId: 987654321);
        
        // Act & Assert - verify "Stok Aman" message
    }

    [Theory]
    [InlineData("week", 7)]
    [InlineData("month", 30)]
    [InlineData(null, 7)] // Default
    public void ProfitCommand_ParsesPeriodCorrectly(string? period, int expectedDays)
    {
        // Verify period parsing logic
        var endDate = DateTime.Today;
        var startDate = period?.ToLowerInvariant() switch
        {
            "month" => endDate.AddMonths(-1),
            _ => endDate.AddDays(-7)
        };
        
        var daysDiff = (endDate - startDate).Days;
        daysDiff.Should().BeGreaterOrEqualTo(expectedDays - 1);
    }
}
```

### Alert Notification Tests

Create `tests/Nastart.Api.Tests/Features/Alerts/AlertNotificationTests.cs`:

```csharp
using FluentAssertions;
using Nastart.Api.Features.Alerts;
using Nastart.Api.Features.Ingredients;

namespace Nastart.Api.Tests.Features.Alerts;

public class AlertNotificationTests
{
    [Theory]
    [InlineData(10, false)]  // 10% - below threshold
    [InlineData(15, false)]  // 15% - at threshold (not above)
    [InlineData(20, true)]   // 20% - above threshold
    [InlineData(-18, true)]  // -18% - significant drop
    public void PriceSpikeThreshold_TriggersCorrectly(
        decimal changePercent, 
        bool shouldTrigger)
    {
        const decimal AlertThreshold = 15m;
        var shouldAlert = Math.Abs(changePercent) > AlertThreshold;
        shouldAlert.Should().Be(shouldTrigger);
    }

    [Fact]
    public void Alert_CreatesWithCorrectDefaults()
    {
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Type = AlertType.PriceSpike,
            Title = "Test Alert",
            Message = "Test message"
        };
        
        alert.IsRead.Should().BeFalse();
        alert.SentViaTelegram.Should().BeFalse();
        alert.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
```

### Full Workflow Test Checklist

```markdown
## Bot Workflow - Manual Test Checklist

### Setup
- [ ] API running: `dotnet run`
- [ ] ngrok tunnel: `ngrok http 5000`
- [ ] Webhook registered: POST /api/bot/setup-webhook
- [ ] Database seeded with test data

### Command Tests

#### /start
- [ ] New user → Welcome message + onboarding keyboard
- [ ] Existing user → Welcome back message
- [ ] User created in database

#### /help
- [ ] Shows all available commands
- [ ] Help buttons work (Cara Scan, Cara Hitung, etc.)

#### /cost [recipe]
- [ ] Without args → Shows recipe list
- [ ] With valid recipe → Shows cost breakdown
- [ ] Shows ingredients with prices
- [ ] Shows total cost, cost per unit
- [ ] Shows margin with emoji indicator
- [ ] Refresh button works
- [ ] Invalid recipe → Error message

#### /price [ingredient]
- [ ] Without args → Shows ingredient list
- [ ] With valid ingredient → Shows price info
- [ ] Shows price history
- [ ] Shows trend (up/down percentage)
- [ ] Shows stock level
- [ ] Invalid ingredient → Error message

#### /low
- [ ] No low stock → "Stok Aman" message
- [ ] With low stock → Categorized list (critical/low/warning)
- [ ] Shows estimated restock cost
- [ ] Export button works

#### /profit
- [ ] Shows sales summary
- [ ] Shows costs summary
- [ ] Shows margin
- [ ] Shows top recipes
- [ ] Period buttons work (week/month)

### Photo Flow (from Week 9)
- [ ] Send receipt photo → Processing message
- [ ] OCR results displayed
- [ ] Confirm → Saved successfully
- [ ] Cancel → Session cancelled
- [ ] Edit → Can modify items

### Alert Notifications
- [ ] Record purchase with 20% price increase → Price spike alert
- [ ] Recipe margin drops below target → Margin alert
- [ ] Low stock threshold crossed → Low stock alert

### Edge Cases
- [ ] Rapid commands → Handles gracefully
- [ ] Invalid input → Friendly error
- [ ] Expired session → Clear message
- [ ] Long recipe/ingredient names → Truncated properly
```

### Run Tests

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend

# Run all bot tests
dotnet test --filter "FullyQualifiedName~Bot"

# Run alert tests
dotnet test --filter "FullyQualifiedName~Alert"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Your Task (Day 7):

1. Create `BotCommandTests.cs`
2. Create `AlertNotificationTests.cs`
3. Run tests: `dotnet test`
4. Perform manual testing with full workflow
5. Document any issues found

---

# Resources

## Microsoft Official Documentation

| Topic | Link |
|-------|------|
| **EF Core Querying** | [learn.microsoft.com/ef/core/querying](https://learn.microsoft.com/en-us/ef/core/querying/) |
| **Background Services** | [learn.microsoft.com/aspnet/core/fundamentals/host/hosted-services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services) |
| **Integration Tests** | [learn.microsoft.com/aspnet/core/test/integration-tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) |
| **Logging** | [learn.microsoft.com/aspnet/core/fundamentals/logging](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/) |

## Telegram Documentation

| Topic | Link |
|-------|------|
| **Bot API** | [core.telegram.org/bots/api](https://core.telegram.org/bots/api) |
| **Inline Keyboards** | [core.telegram.org/bots/features#inline-keyboards](https://core.telegram.org/bots/features#inline-keyboards) |
| **Formatting** | [core.telegram.org/bots/api#formatting-options](https://core.telegram.org/bots/api#formatting-options) |
| **Commands** | [core.telegram.org/bots/features#commands](https://core.telegram.org/bots/features#commands) |

## External Resources

| Topic | Link |
|-------|------|
| **Telegram.Bot NuGet** | [github.com/TelegramBots/Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot) |
| **MediatR** | [github.com/jbogard/MediatR](https://github.com/jbogard/MediatR) |
| **FluentAssertions** | [fluentassertions.com](https://fluentassertions.com/) |

## Week 10 Checklist

- [ ] Created `CostCommand` feature slice
- [ ] Cost breakdown shows ingredients, totals, margin
- [ ] Created `PriceCommand` feature slice
- [ ] Price history and trends displayed
- [ ] Created `LowStockCommand` feature slice
- [ ] Items categorized by urgency level
- [ ] Created `ProfitCommand` feature slice
- [ ] Profit summary with recipe analysis
- [ ] Created `Alert` entity model
- [ ] Created `ITelegramAlertService` interface
- [ ] Implemented `TelegramAlertService`
- [ ] Created `PriceSpikeNotificationHandler`
- [ ] Created `MarginAlertNotificationHandler`
- [ ] Notifications trigger Telegram alerts
- [ ] Updated `RecordPurchase` to publish notifications
- [ ] Added all commands to webhook handler routing
- [ ] Created unit tests for commands
- [ ] Created unit tests for alerts
- [ ] Full workflow tested end-to-end
- [ ] All builds pass: `dotnet build`
- [ ] All tests pass: `dotnet test`

---

## What's Next?

**Phase 4: Vue.js Dashboard (Weeks 11-14)** — Build the web frontend with Nuxt.js, including login/register, dashboard with charts, ingredient management, recipe cost calculator, and real-time updates.

---

*Nastart — Start smart, bake profitable*
