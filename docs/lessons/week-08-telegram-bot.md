# Week 8: Telegram Bot Fundamentals

> **Objective**: Build the `Nastart.Bot` project with webhook controller, command routing, and inline keyboards using .NET 10 best practices.

## Prerequisites
- Completed Week 7 (API Layer + PaddleOCR microservice)
- Telegram account for BotFather setup
- Understanding of ASP.NET Core controllers and dependency injection

---

## Day 1: BotFather Setup & Telegram Bot API Concepts

### Learning Objectives
- Create a Telegram bot via BotFather
- Understand Telegram Bot API concepts: webhooks vs polling
- Configure bot tokens securely

### Tasks

#### 1.1 Create Your Bot with BotFather
Open Telegram and search for `@BotFather`, then follow these steps:

```
/newbot
→ Name: Nastart Bot
→ Username: nastart_yourname_bot (must end with 'bot')
```

Save the token BotFather gives you — this is your `TELEGRAM_BOT_TOKEN`.

#### 1.2 Understand Webhook vs Polling

| Approach | Description | Use Case |
|----------|-------------|----------|
| **Polling** | Bot repeatedly asks Telegram "any new messages?" | Development, simple bots |
| **Webhook** | Telegram pushes updates to your server | Production, scalable apps |

> **Nastart uses webhooks** — more efficient for production and fits ASP.NET Core's request/response model.

#### 1.3 Configure Bot Token Securely

Add to `appsettings.Development.json`:

```json
{
  "Telegram": {
    "BotToken": "your-bot-token-here",
    "WebhookUrl": "https://your-ngrok-url/api/telegram/webhook"
  }
}
```

Create the options class in `Nastart.Bot`:

```csharp
namespace Nastart.Bot.Configuration;

public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";
    
    public required string BotToken { get; init; }
    public required string WebhookUrl { get; init; }
}
```

### References
- [Telegram Bot API Documentation](https://core.telegram.org/bots/api)
- [BotFather Guide](https://core.telegram.org/bots#botfather)

---

## Day 2: Webhook Controller Implementation

### Learning Objectives
- Create a webhook endpoint for Telegram updates
- Process incoming messages asynchronously
- Return 200 OK quickly to avoid Telegram retries

### Tasks

#### 2.1 Install Telegram.Bot NuGet Package

```powershell
cd backend/src/Nastart.Bot
dotnet add package Telegram.Bot
```

#### 2.2 Create the Webhook Controller

Following Microsoft's webhook controller patterns — return quickly, process asynchronously:

```csharp
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot.Types;
using Nastart.Bot.Routing;

namespace Nastart.Bot.Controllers;

[ApiController]
[Route("api/telegram")]
public sealed class TelegramWebhookController : ControllerBase
{
    private readonly IUpdateRouter _router;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(
        IUpdateRouter router,
        ILogger<TelegramWebhookController> logger)
    {
        _router = router;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleUpdate(
        [FromBody] Update update,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received update {UpdateId} of type {UpdateType}",
            update.Id,
            update.Type);

        try
        {
            // Process the update (routes to appropriate handler)
            await _router.RouteAsync(update, cancellationToken);
        }
        catch (Exception ex)
        {
            // Log but don't throw — always return 200 to Telegram
            _logger.LogError(ex, "Error processing update {UpdateId}", update.Id);
        }

        // Always return OK to prevent Telegram from retrying
        return Ok();
    }
}
```

> **Best Practice**: Always return 200 OK to Telegram, even on errors. Log errors internally but don't expose them.

#### 2.3 Create the Update Router Interface

```csharp
using Telegram.Bot.Types;

namespace Nastart.Bot.Routing;

public interface IUpdateRouter
{
    Task RouteAsync(Update update, CancellationToken cancellationToken = default);
}
```

### References
- [ASP.NET Core Controller Basics](https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-10.0)
- [Webhook Best Practices](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0)

---

## Day 3: Command Routing Architecture

### Learning Objectives
- Implement the Command pattern for bot commands
- Route messages to appropriate handlers
- Create `/start` and `/help` commands

### Tasks

#### 3.1 Create the Command Handler Interface

```csharp
using Telegram.Bot.Types;

namespace Nastart.Bot.Commands;

public interface ICommandHandler
{
    string Command { get; }
    Task HandleAsync(Message message, CancellationToken cancellationToken = default);
}
```

#### 3.2 Implement the Command Router

```csharp
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Nastart.Bot.Commands;
using Nastart.Bot.Handlers;

namespace Nastart.Bot.Routing;

public sealed class UpdateRouter : IUpdateRouter
{
    private readonly IEnumerable<ICommandHandler> _commandHandlers;
    private readonly ITextHandler _textHandler;
    private readonly IPhotoHandler _photoHandler;
    private readonly ICallbackHandler _callbackHandler;
    private readonly ILogger<UpdateRouter> _logger;

    public UpdateRouter(
        IEnumerable<ICommandHandler> commandHandlers,
        ITextHandler textHandler,
        IPhotoHandler photoHandler,
        ICallbackHandler callbackHandler,
        ILogger<UpdateRouter> logger)
    {
        _commandHandlers = commandHandlers;
        _textHandler = textHandler;
        _photoHandler = photoHandler;
        _callbackHandler = callbackHandler;
        _logger = logger;
    }

    public async Task RouteAsync(Update update, CancellationToken cancellationToken = default)
    {
        var handler = update.Type switch
        {
            UpdateType.Message when update.Message?.Text?.StartsWith('/') == true
                => RouteCommand(update.Message, cancellationToken),
            
            UpdateType.Message when update.Message?.Photo is not null
                => _photoHandler.HandleAsync(update.Message, cancellationToken),
            
            UpdateType.Message when update.Message?.Text is not null
                => _textHandler.HandleAsync(update.Message, cancellationToken),
            
            UpdateType.CallbackQuery when update.CallbackQuery is not null
                => _callbackHandler.HandleAsync(update.CallbackQuery, cancellationToken),
            
            _ => Task.CompletedTask
        };

        await handler;
    }

    private async Task RouteCommand(Message message, CancellationToken cancellationToken)
    {
        var commandText = message.Text!.Split(' ')[0].TrimStart('/').ToLowerInvariant();
        
        // Handle commands with @botname suffix
        var atIndex = commandText.IndexOf('@');
        if (atIndex > 0)
        {
            commandText = commandText[..atIndex];
        }

        var handler = _commandHandlers.FirstOrDefault(h => 
            h.Command.Equals(commandText, StringComparison.OrdinalIgnoreCase));

        if (handler is not null)
        {
            await handler.HandleAsync(message, cancellationToken);
        }
        else
        {
            _logger.LogWarning("Unknown command: {Command}", commandText);
        }
    }
}
```

#### 3.3 Create Start and Help Commands

```csharp
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Nastart.Bot.Commands;

public sealed class StartCommand : ICommandHandler
{
    private readonly ITelegramBotClient _botClient;

    public StartCommand(ITelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    public string Command => "start";

    public async Task HandleAsync(Message message, CancellationToken cancellationToken = default)
    {
        var welcomeText = """
            🍳 *Welcome to Nastart!*
            
            I help you track ingredient costs and recipe margins.
            
            📸 Send me a receipt photo to log purchases
            💰 Use /cost [recipe] to check recipe costs
            📊 Use /price [ingredient] to see price history
            ⚠️ Use /low to see low stock items
            
            Ready to start? Send me a receipt photo!
            """;

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: welcomeText,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            cancellationToken: cancellationToken);
    }
}
```

```csharp
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Nastart.Bot.Commands;

public sealed class HelpCommand : ICommandHandler
{
    private readonly ITelegramBotClient _botClient;

    public HelpCommand(ITelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    public string Command => "help";

    public async Task HandleAsync(Message message, CancellationToken cancellationToken = default)
    {
        var helpText = """
            📋 *Nastart Commands*
            
            *Receipt Scanning*
            📸 Just send a photo of your receipt
            
            *Recipe Management*
            /cost [recipe] — Check recipe cost and margin
            /recipes — List all recipes
            
            *Inventory*
            /price [ingredient] — View price history
            /low — Show low stock items
            /stock — Current inventory levels
            
            *Reports*
            /profit — Today's profit summary
            /weekly — Weekly report
            
            Need help? Contact @your_support
            """;

        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: helpText,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            cancellationToken: cancellationToken);
    }
}
```

### References
- [Command Pattern](https://refactoring.guru/design-patterns/command)
- [.NET Dependency Injection for Multiple Implementations](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)

---

## Day 4: Inline Keyboards & Callback Handling

### Learning Objectives
- Create inline keyboard menus
- Handle callback queries from button presses
- Implement pagination for long lists

### Tasks

#### 4.1 Create Keyboard Builder Service

```csharp
using Telegram.Bot.Types.ReplyMarkups;

namespace Nastart.Bot.Keyboards;

public interface IKeyboardBuilder
{
    InlineKeyboardMarkup BuildMainMenu();
    InlineKeyboardMarkup BuildConfirmation(string confirmData, string cancelData);
    InlineKeyboardMarkup BuildPaginated<T>(
        IEnumerable<T> items,
        Func<T, string> textSelector,
        Func<T, string> callbackSelector,
        int currentPage,
        int pageSize = 5);
}

public sealed class KeyboardBuilder : IKeyboardBuilder
{
    public InlineKeyboardMarkup BuildMainMenu()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📸 Scan Receipt", "action:scan"),
                InlineKeyboardButton.WithCallbackData("📊 View Reports", "action:reports")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🍳 Recipes", "action:recipes"),
                InlineKeyboardButton.WithCallbackData("📦 Inventory", "action:inventory")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⚙️ Settings", "action:settings")
            }
        });
    }

    public InlineKeyboardMarkup BuildConfirmation(string confirmData, string cancelData)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Confirm", confirmData),
                InlineKeyboardButton.WithCallbackData("❌ Cancel", cancelData)
            }
        });
    }

    public InlineKeyboardMarkup BuildPaginated<T>(
        IEnumerable<T> items,
        Func<T, string> textSelector,
        Func<T, string> callbackSelector,
        int currentPage,
        int pageSize = 5)
    {
        var itemList = items.ToList();
        var totalPages = (int)Math.Ceiling(itemList.Count / (double)pageSize);
        var pageItems = itemList
            .Skip(currentPage * pageSize)
            .Take(pageSize)
            .ToList();

        var buttons = new List<InlineKeyboardButton[]>();

        // Item buttons (one per row)
        foreach (var item in pageItems)
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    textSelector(item),
                    callbackSelector(item))
            });
        }

        // Navigation row
        var navButtons = new List<InlineKeyboardButton>();
        
        if (currentPage > 0)
        {
            navButtons.Add(InlineKeyboardButton.WithCallbackData(
                "⬅️ Previous",
                $"page:{currentPage - 1}"));
        }
        
        navButtons.Add(InlineKeyboardButton.WithCallbackData(
            $"{currentPage + 1}/{totalPages}",
            "page:current"));
        
        if (currentPage < totalPages - 1)
        {
            navButtons.Add(InlineKeyboardButton.WithCallbackData(
                "Next ➡️",
                $"page:{currentPage + 1}"));
        }

        buttons.Add(navButtons.ToArray());

        return new InlineKeyboardMarkup(buttons);
    }
}
```

#### 4.2 Create Callback Handler

```csharp
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Nastart.Bot.Handlers;

public interface ICallbackHandler
{
    Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken = default);
}

public sealed class CallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<CallbackHandler> _logger;

    public CallbackHandler(
        ITelegramBotClient botClient,
        ILogger<CallbackHandler> logger)
    {
        _botClient = botClient;
        _logger = logger;
    }

    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken = default)
    {
        var data = callbackQuery.Data ?? string.Empty;
        _logger.LogInformation("Callback received: {Data}", data);

        // Parse callback data (format: "action:value")
        var parts = data.Split(':');
        var action = parts[0];
        var value = parts.Length > 1 ? parts[1] : string.Empty;

        // Always answer the callback to remove loading state
        await _botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQuery.Id,
            cancellationToken: cancellationToken);

        await (action switch
        {
            "action" => HandleAction(callbackQuery, value, cancellationToken),
            "recipe" => HandleRecipeSelection(callbackQuery, value, cancellationToken),
            "confirm" => HandleConfirmation(callbackQuery, value, cancellationToken),
            "page" => HandlePagination(callbackQuery, value, cancellationToken),
            _ => Task.CompletedTask
        });
    }

    private async Task HandleAction(
        CallbackQuery callbackQuery, 
        string action, 
        CancellationToken cancellationToken)
    {
        var responseText = action switch
        {
            "scan" => "📸 Send me a photo of your receipt to scan.",
            "reports" => "📊 Select a report type...",
            "recipes" => "🍳 Loading your recipes...",
            "inventory" => "📦 Loading inventory...",
            "settings" => "⚙️ Opening settings...",
            _ => "Unknown action"
        };

        await _botClient.SendMessage(
            chatId: callbackQuery.Message!.Chat.Id,
            text: responseText,
            cancellationToken: cancellationToken);
    }

    private Task HandleRecipeSelection(
        CallbackQuery callbackQuery, 
        string recipeId, 
        CancellationToken cancellationToken)
    {
        // TODO: Implement with MediatR query
        return Task.CompletedTask;
    }

    private Task HandleConfirmation(
        CallbackQuery callbackQuery, 
        string action, 
        CancellationToken cancellationToken)
    {
        // TODO: Implement confirmation logic
        return Task.CompletedTask;
    }

    private Task HandlePagination(
        CallbackQuery callbackQuery, 
        string page, 
        CancellationToken cancellationToken)
    {
        // TODO: Implement pagination
        return Task.CompletedTask;
    }
}
```

### References
- [Telegram Inline Keyboards](https://core.telegram.org/bots/api#inlinekeyboardmarkup)
- [Callback Query Handling](https://core.telegram.org/bots/api#callbackquery)

---

## Day 5: Photo Message Handling

### Learning Objectives
- Detect and download photo messages
- Send photos to the OCR service
- Provide user feedback during processing

### Tasks

#### 5.1 Create Photo Handler

```csharp
using Telegram.Bot;
using Telegram.Bot.Types;
using Nastart.Bot.Keyboards;

namespace Nastart.Bot.Handlers;

public interface IPhotoHandler
{
    Task HandleAsync(Message message, CancellationToken cancellationToken = default);
}

public sealed class PhotoHandler : IPhotoHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly IKeyboardBuilder _keyboardBuilder;
    private readonly ILogger<PhotoHandler> _logger;

    public PhotoHandler(
        ITelegramBotClient botClient,
        IKeyboardBuilder keyboardBuilder,
        ILogger<PhotoHandler> logger)
    {
        _botClient = botClient;
        _keyboardBuilder = keyboardBuilder;
        _logger = logger;
    }

    public async Task HandleAsync(Message message, CancellationToken cancellationToken = default)
    {
        if (message.Photo is null || message.Photo.Length == 0)
        {
            return;
        }

        var chatId = message.Chat.Id;

        // Get the largest photo size (last in array)
        var photo = message.Photo[^1];
        
        _logger.LogInformation(
            "Received photo {FileId} from chat {ChatId}, size: {Width}x{Height}",
            photo.FileId,
            chatId,
            photo.Width,
            photo.Height);

        // Send processing message
        var processingMessage = await _botClient.SendMessage(
            chatId: chatId,
            text: "🔄 Processing your receipt...",
            cancellationToken: cancellationToken);

        try
        {
            // Download the file
            var file = await _botClient.GetFile(photo.FileId, cancellationToken);
            
            if (file.FilePath is null)
            {
                throw new InvalidOperationException("Could not get file path from Telegram");
            }

            using var memoryStream = new MemoryStream();
            await _botClient.DownloadFile(file.FilePath, memoryStream, cancellationToken);
            var imageBytes = memoryStream.ToArray();

            // TODO: Send to PaddleOCR service via MediatR
            // var command = new ScanReceiptCommand(imageBytes, chatId);
            // var result = await _mediator.Send(command, cancellationToken);

            // Simulated response for now
            await _botClient.EditMessageText(
                chatId: chatId,
                messageId: processingMessage.MessageId,
                text: """
                    ✅ *Receipt Scanned Successfully!*
                    
                    Found items:
                    • Tomatoes - 5kg @ ₦2,500
                    • Onions - 3kg @ ₦1,800
                    • Palm Oil - 2L @ ₦4,000
                    
                    *Total: ₦8,300*
                    
                    Save this purchase?
                    """,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: _keyboardBuilder.BuildConfirmation(
                    "confirm:purchase",
                    "confirm:cancel"),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing photo from chat {ChatId}", chatId);

            await _botClient.EditMessageText(
                chatId: chatId,
                messageId: processingMessage.MessageId,
                text: "❌ Sorry, I couldn't process that receipt. Please try again with a clearer photo.",
                cancellationToken: cancellationToken);
        }
    }
}
```

#### 5.2 Create Text Handler (for non-command messages)

```csharp
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Nastart.Bot.Handlers;

public interface ITextHandler
{
    Task HandleAsync(Message message, CancellationToken cancellationToken = default);
}

public sealed class TextHandler : ITextHandler
{
    private readonly ITelegramBotClient _botClient;

    public TextHandler(ITelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    public async Task HandleAsync(Message message, CancellationToken cancellationToken = default)
    {
        // Handle free-form text input
        // Could be ingredient search, recipe name, etc.
        
        await _botClient.SendMessage(
            chatId: message.Chat.Id,
            text: "💡 *Tip:* Send me a receipt photo to log purchases, or use /help to see available commands.",
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            cancellationToken: cancellationToken);
    }
}
```

### References
- [Telegram File Download](https://core.telegram.org/bots/api#getfile)
- [Photo Message Handling](https://core.telegram.org/bots/api#photosize)

---

## Day 6: Background Services & Webhook Registration

### Learning Objectives
- Register webhook on application startup using `IHostedService`
- Create a background service for processing updates
- Use `Channel<T>` for producer-consumer pattern

### Tasks

#### 6.1 Create Webhook Registration Service

Using the `IHostedService` pattern from Microsoft Learn:

```csharp
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Nastart.Bot.Configuration;

namespace Nastart.Bot.Services;

public sealed class WebhookRegistrationService : IHostedService
{
    private readonly ITelegramBotClient _botClient;
    private readonly TelegramOptions _options;
    private readonly ILogger<WebhookRegistrationService> _logger;

    public WebhookRegistrationService(
        ITelegramBotClient botClient,
        IOptions<TelegramOptions> options,
        ILogger<WebhookRegistrationService> logger)
    {
        _botClient = botClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Registering Telegram webhook at {WebhookUrl}", _options.WebhookUrl);

        await _botClient.SetWebhook(
            url: _options.WebhookUrl,
            allowedUpdates: [],  // Receive all update types
            cancellationToken: cancellationToken);

        _logger.LogInformation("Telegram webhook registered successfully");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Removing Telegram webhook");

        await _botClient.DeleteWebhook(cancellationToken: cancellationToken);

        _logger.LogInformation("Telegram webhook removed");
    }
}
```

#### 6.2 Create Background Update Queue (Optional - for heavy processing)

Using `Channel<T>` for producer-consumer pattern:

```csharp
using System.Threading.Channels;
using Telegram.Bot.Types;

namespace Nastart.Bot.Services;

public interface IUpdateQueue
{
    ValueTask QueueAsync(Update update, CancellationToken cancellationToken = default);
    ValueTask<Update> DequeueAsync(CancellationToken cancellationToken = default);
}

public sealed class UpdateQueue : IUpdateQueue
{
    private readonly Channel<Update> _queue;

    public UpdateQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<Update>(options);
    }

    public async ValueTask QueueAsync(Update update, CancellationToken cancellationToken = default)
    {
        await _queue.Writer.WriteAsync(update, cancellationToken);
    }

    public async ValueTask<Update> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}
```

#### 6.3 Create Background Processing Service

```csharp
using Nastart.Bot.Routing;

namespace Nastart.Bot.Services;

public sealed class UpdateProcessingService : BackgroundService
{
    private readonly IUpdateQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UpdateProcessingService> _logger;

    public UpdateProcessingService(
        IUpdateQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<UpdateProcessingService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Update processing service is running");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var update = await _queue.DequeueAsync(stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var router = scope.ServiceProvider.GetRequiredService<IUpdateRouter>();

                await router.RouteAsync(update, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing update from queue");
            }
        }

        _logger.LogInformation("Update processing service is stopping");
    }
}
```

#### 6.4 Configure Dependency Injection

In `Program.cs` or a dedicated extension method:

```csharp
using Telegram.Bot;
using Nastart.Bot.Configuration;
using Nastart.Bot.Commands;
using Nastart.Bot.Handlers;
using Nastart.Bot.Keyboards;
using Nastart.Bot.Routing;
using Nastart.Bot.Services;

namespace Nastart.Bot.Extensions;

public static class TelegramBotExtensions
{
    public static IServiceCollection AddTelegramBot(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind options
        services.Configure<TelegramOptions>(
            configuration.GetSection(TelegramOptions.SectionName));

        // Register Telegram.Bot client
        services.AddHttpClient("telegram_bot")
            .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
            {
                var options = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
                return new TelegramBotClient(options.BotToken, httpClient);
            });

        // Register services
        services.AddSingleton<IUpdateQueue, UpdateQueue>();
        services.AddSingleton<IKeyboardBuilder, KeyboardBuilder>();
        
        // Register handlers
        services.AddScoped<IUpdateRouter, UpdateRouter>();
        services.AddScoped<ITextHandler, TextHandler>();
        services.AddScoped<IPhotoHandler, PhotoHandler>();
        services.AddScoped<ICallbackHandler, CallbackHandler>();
        
        // Register command handlers
        services.AddScoped<ICommandHandler, StartCommand>();
        services.AddScoped<ICommandHandler, HelpCommand>();

        // Register hosted services
        services.AddHostedService<WebhookRegistrationService>();
        services.AddHostedService<UpdateProcessingService>();

        return services;
    }
}
```

### References
- [Background Tasks with IHostedService](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0)
- [Queued Background Tasks](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0#queued-background-tasks)
- [Channel<T> Producer-Consumer](https://learn.microsoft.com/en-us/dotnet/core/extensions/queue-service)

---

## Day 7: Testing & Error Handling

### Learning Objectives
- Test bot components with unit tests
- Implement proper error handling and logging
- Set up ngrok for local development testing

### Tasks

#### 7.1 Create Bot Test Project

```powershell
cd backend/tests
dotnet new xunit -n Nastart.Bot.Tests
dotnet sln ../Nastart.sln add Nastart.Bot.Tests/Nastart.Bot.Tests.csproj
cd Nastart.Bot.Tests
dotnet add reference ../../src/Nastart.Bot/Nastart.Bot.csproj
dotnet add package NSubstitute
dotnet add package FluentAssertions
```

#### 7.2 Test Command Routing

```csharp
using FluentAssertions;
using NSubstitute;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Nastart.Bot.Commands;
using Nastart.Bot.Handlers;
using Nastart.Bot.Routing;
using Microsoft.Extensions.Logging;

namespace Nastart.Bot.Tests;

public class UpdateRouterTests
{
    private readonly ITelegramBotClient _botClient;
    private readonly IUpdateRouter _router;
    private readonly ICommandHandler _startCommand;
    private readonly ICommandHandler _helpCommand;

    public UpdateRouterTests()
    {
        _botClient = Substitute.For<ITelegramBotClient>();
        
        _startCommand = new StartCommand(_botClient);
        _helpCommand = new HelpCommand(_botClient);

        var commandHandlers = new[] { _startCommand, _helpCommand };
        var textHandler = Substitute.For<ITextHandler>();
        var photoHandler = Substitute.For<IPhotoHandler>();
        var callbackHandler = Substitute.For<ICallbackHandler>();
        var logger = Substitute.For<ILogger<UpdateRouter>>();

        _router = new UpdateRouter(
            commandHandlers,
            textHandler,
            photoHandler,
            callbackHandler,
            logger);
    }

    [Fact]
    public async Task RouteAsync_StartCommand_InvokesStartHandler()
    {
        // Arrange
        var update = new Update
        {
            Id = 1,
            Message = new Message
            {
                MessageId = 1,
                Chat = new Chat { Id = 123, Type = ChatType.Private },
                Text = "/start",
                Date = DateTime.UtcNow
            }
        };

        // Act
        await _router.RouteAsync(update);

        // Assert
        await _botClient.Received(1).SendMessage(
            chatId: 123,
            text: Arg.Is<string>(s => s.Contains("Welcome to Nastart")),
            parseMode: ParseMode.Markdown,
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteAsync_CommandWithBotUsername_StripsUsername()
    {
        // Arrange
        var update = new Update
        {
            Id = 1,
            Message = new Message
            {
                MessageId = 1,
                Chat = new Chat { Id = 123, Type = ChatType.Private },
                Text = "/help@nastart_bot",
                Date = DateTime.UtcNow
            }
        };

        // Act
        await _router.RouteAsync(update);

        // Assert
        await _botClient.Received(1).SendMessage(
            chatId: 123,
            text: Arg.Is<string>(s => s.Contains("Nastart Commands")),
            parseMode: ParseMode.Markdown,
            cancellationToken: Arg.Any<CancellationToken>());
    }
}
```

#### 7.3 Test Photo Handler

```csharp
using FluentAssertions;
using NSubstitute;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Nastart.Bot.Handlers;
using Nastart.Bot.Keyboards;
using Microsoft.Extensions.Logging;

namespace Nastart.Bot.Tests;

public class PhotoHandlerTests
{
    private readonly ITelegramBotClient _botClient;
    private readonly IKeyboardBuilder _keyboardBuilder;
    private readonly PhotoHandler _handler;

    public PhotoHandlerTests()
    {
        _botClient = Substitute.For<ITelegramBotClient>();
        _keyboardBuilder = new KeyboardBuilder();
        var logger = Substitute.For<ILogger<PhotoHandler>>();

        _handler = new PhotoHandler(_botClient, _keyboardBuilder, logger);
    }

    [Fact]
    public async Task HandleAsync_WithPhoto_SendsProcessingMessage()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            Chat = new Chat { Id = 123, Type = ChatType.Private },
            Photo = new[]
            {
                new PhotoSize { FileId = "small", Width = 100, Height = 100, FileUniqueId = "1" },
                new PhotoSize { FileId = "large", Width = 800, Height = 600, FileUniqueId = "2" }
            },
            Date = DateTime.UtcNow
        };

        _botClient.SendMessage(
            chatId: Arg.Any<ChatId>(),
            text: Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new Message { MessageId = 2, Chat = message.Chat, Date = DateTime.UtcNow });

        _botClient.GetFile(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new Telegram.Bot.Types.File { FileId = "large", FilePath = "photos/file.jpg" });

        // Act
        await _handler.HandleAsync(message);

        // Assert
        await _botClient.Received(1).SendMessage(
            chatId: 123,
            text: Arg.Is<string>(s => s.Contains("Processing")),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithoutPhoto_DoesNothing()
    {
        // Arrange
        var message = new Message
        {
            MessageId = 1,
            Chat = new Chat { Id = 123, Type = ChatType.Private },
            Photo = null,
            Date = DateTime.UtcNow
        };

        // Act
        await _handler.HandleAsync(message);

        // Assert
        await _botClient.DidNotReceive().SendMessage(
            chatId: Arg.Any<ChatId>(),
            text: Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }
}
```

#### 7.4 Set Up ngrok for Local Testing

```powershell
# Install ngrok (if not installed)
winget install ngrok.ngrok

# Start ngrok tunnel to your local API
ngrok http 5000

# Copy the HTTPS URL and update appsettings.Development.json
# Example: https://abc123.ngrok.io/api/telegram/webhook
```

#### 7.5 Implement Global Error Handling Middleware

```csharp
namespace Nastart.Bot.Middleware;

public sealed class TelegramErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TelegramErrorHandlingMiddleware> _logger;

    public TelegramErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<TelegramErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Log the error but don't throw
            // Telegram webhook endpoints should always return 200
            if (context.Request.Path.StartsWithSegments("/api/telegram"))
            {
                _logger.LogError(ex, "Error in Telegram webhook endpoint");
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("OK");
            }
            else
            {
                throw;
            }
        }
    }
}
```

### References
- [xUnit Testing](https://xunit.net/)
- [NSubstitute Mocking](https://nsubstitute.github.io/)
- [ngrok Documentation](https://ngrok.com/docs)

---

## Week 8 Summary

### Files Created

```
Nastart.Bot/
├── Configuration/
│   └── TelegramOptions.cs
├── Controllers/
│   └── TelegramWebhookController.cs
├── Commands/
│   ├── ICommandHandler.cs
│   ├── StartCommand.cs
│   └── HelpCommand.cs
├── Handlers/
│   ├── ITextHandler.cs
│   ├── IPhotoHandler.cs
│   ├── ICallbackHandler.cs
│   ├── TextHandler.cs
│   ├── PhotoHandler.cs
│   └── CallbackHandler.cs
├── Keyboards/
│   ├── IKeyboardBuilder.cs
│   └── KeyboardBuilder.cs
├── Middleware/
│   └── TelegramErrorHandlingMiddleware.cs
├── Routing/
│   ├── IUpdateRouter.cs
│   └── UpdateRouter.cs
├── Services/
│   ├── IUpdateQueue.cs
│   ├── UpdateQueue.cs
│   ├── WebhookRegistrationService.cs
│   └── UpdateProcessingService.cs
└── Extensions/
    └── TelegramBotExtensions.cs
```

### Key Concepts Learned
1. **Webhook Controller** — Handle Telegram updates via HTTP POST, always return 200 OK
2. **Command Routing** — Route bot commands to appropriate handlers
3. **Inline Keyboards** — Create interactive button menus
4. **Callback Handling** — Process button press callbacks
5. **Photo Handling** — Download and process user photos
6. **Background Services** — Use `IHostedService` for webhook registration
7. **Producer-Consumer** — Use `Channel<T>` for async processing

### Anti-Patterns Avoided
- ❌ Blocking operations in webhook handler
- ❌ Returning error status codes to Telegram
- ❌ No error handling in callbacks
- ❌ Hardcoded bot tokens in code

### Next Week Preview
**Week 9: Receipt Photo Handling** — Connect photo handler to PaddleOCR service, implement ingredient matching, and create confirmation workflows.
