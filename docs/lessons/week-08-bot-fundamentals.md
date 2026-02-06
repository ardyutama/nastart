# Week 8: Bot Fundamentals 🤖

> **Goal**: Create your Telegram bot, understand the Bot API, implement webhook endpoints using Vertical Slice Architecture, and build your first bot commands (`/start`, `/help`) with proper command routing.

---

## Table of Contents
1. [Day 1: Create Telegram Bot via BotFather](#day-1-create-telegram-bot-via-botfather)
2. [Day 2: Telegram Bot API — Webhooks & Message Types](#day-2-telegram-bot-api--webhooks--message-types)
3. [Day 3: TelegramWebhookEndpoint Feature](#day-3-telegramwebhookendpoint-feature)
4. [Day 4: StartCommand Feature Slice](#day-4-startcommand-feature-slice)
5. [Day 5: HelpCommand Feature Slice](#day-5-helpcommand-feature-slice)
6. [Day 6: Command Routing & Dispatcher](#day-6-command-routing--dispatcher)
7. [Day 7: Testing Bot Features](#day-7-testing-bot-features)
8. [Resources](#resources) *(Microsoft Official Docs Verified)*

---

# Day 1: Create Telegram Bot via BotFather

## 🧒 Explain Like I'm 5

Imagine you want to build a robot helper 🤖 that talks to people on Telegram. First, you need to ask the "Robot Factory" (BotFather) to create one for you!

BotFather is like a magic wizard in Telegram who:
- Creates new bots and gives them names
- Gives you a **secret key** (token) to control your bot
- Lets you set your bot's picture and description

After BotFather creates your bot, you get a special password called a **token** — guard it like treasure! Anyone with this token can control your bot!

## 🔧 Engineer Language

**BotFather** is Telegram's official bot for creating and managing bots. It provides you with an **API token** — a unique identifier that authenticates your application to the Telegram Bot API.

> 📖 **Telegram Docs**: *"BotFather is the one bot to rule them all. Use it to create new bot accounts and manage your existing bots."*
>
> — [Telegram BotFather](https://core.telegram.org/bots#botfather)

### Step-by-Step: Create Your Bot

1. **Open Telegram** and search for `@BotFather`
2. **Start a conversation**: Send `/start`
3. **Create new bot**: Send `/newbot`
4. **Name your bot**: "Nastart Bot" (display name)
5. **Choose username**: `nastart_dev_bot` (must end with `bot`)
6. **Save your token**: BotFather gives you something like:
   ```
   123456789:ABCdefGHIjklMNOpqrsTUVwxyz
   ```

### Configure Your Bot

After creation, send these commands to BotFather:

```
/setdescription
Select your bot, then enter:
"Smart inventory & finance tracking for bakeries. Track ingredients, scan receipts, calculate recipe costs!"

/setabouttext
Select your bot, then enter:
"Nastart helps small F&B businesses track inventory, scan receipts with OCR, and calculate real-time recipe costs."

/setcommands
Select your bot, then enter:
start - Start using Nastart
help - Show available commands
cost - Calculate recipe cost
price - Check ingredient price
low - Show low stock items
scan - Scan a receipt photo
profit - View profit summary
```

### Store Token Securely

**NEVER commit your token to Git!**

Add to `appsettings.Development.json`:

```json
{
  "Telegram": {
    "BotToken": "YOUR_BOT_TOKEN_HERE",
    "WebhookUrl": "https://your-domain.com/api/bot/webhook",
    "SecretToken": "your-random-secret-for-webhook-validation"
  }
}
```

Create `src/Nastart.Api/Shared/Configuration/TelegramOptions.cs`:

```csharp
namespace Nastart.Api.Shared.Configuration;

/// <summary>
/// Configuration options for Telegram bot.
/// </summary>
public class TelegramOptions
{
    public const string SectionName = "Telegram";
    
    /// <summary>
    /// Bot API token from BotFather.
    /// </summary>
    public required string BotToken { get; set; }
    
    /// <summary>
    /// Public URL for webhook endpoint.
    /// </summary>
    public required string WebhookUrl { get; set; }
    
    /// <summary>
    /// Secret token for validating webhook requests.
    /// </summary>
    public string? SecretToken { get; set; }
    
    /// <summary>
    /// Allowed update types to receive.
    /// </summary>
    public string[] AllowedUpdates { get; set; } = 
        ["message", "callback_query", "inline_query"];
}
```

### Your Task (Day 1):

```powershell
# 1. Create your bot via BotFather in Telegram
# 2. Save the token in appsettings.Development.json

cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create Bot feature folder
mkdir Features\Bot

# Create configuration folder
mkdir Shared\Configuration -ErrorAction SilentlyContinue

# Create configuration file
New-Item Shared\Configuration\TelegramOptions.cs

# Verify build
dotnet build
```

---

# Day 2: Telegram Bot API — Webhooks & Message Types

## 🧒 Explain Like I'm 5

Your robot helper needs to LISTEN for messages! There are two ways:

**Polling** (asking again and again):
- Robot: "Any messages?" → Telegram: "Nope"
- Robot: "Any messages?" → Telegram: "Nope"
- Robot: "Any messages?" → Telegram: "Yes! Here's one!"
- (Wastes energy asking constantly!)

**Webhooks** (getting a phone call):
- Telegram: "Hey robot, someone sent you a message!" 📞
- Robot: "Thanks, I'll handle it!"
- (Much better — only wakes up when needed!)

We'll use **webhooks** because they're faster and use less resources!

## 🔧 Engineer Language

**Webhooks** are HTTP callbacks — Telegram sends a POST request to your server whenever there's an update. This is more efficient than polling, especially for production.

> 📖 **Microsoft Docs**: *"Webhooks are user-defined HTTP callbacks triggered by specific events. When an event occurs, the source site makes an HTTP request to the URL configured for the webhook."*
>
> — [ASP.NET Core webhooks](https://learn.microsoft.com/en-us/aspnet/webhooks/)

### Webhook vs Polling

| Aspect | Webhook | Polling |
|--------|---------|---------|
| **Latency** | Instant | Depends on interval |
| **Resource Usage** | Low (on-demand) | High (constant requests) |
| **Complexity** | Needs public HTTPS URL | Works locally |
| **Best For** | Production | Development/testing |

### Update Types

Telegram sends different update types:

| Update Type | Description | Example |
|-------------|-------------|---------|
| `message` | Text, photo, document, etc. | User sends "/start" |
| `callback_query` | Inline keyboard button click | User clicks "Confirm" |
| `inline_query` | Inline bot query | User types "@bot query" |
| `edited_message` | Edited message | User modifies sent message |

### Message Structure

```json
{
  "update_id": 123456789,
  "message": {
    "message_id": 1,
    "from": {
      "id": 987654321,
      "is_bot": false,
      "first_name": "Ibu",
      "last_name": "Sari",
      "username": "ibusari",
      "language_code": "id"
    },
    "chat": {
      "id": 987654321,
      "first_name": "Ibu",
      "last_name": "Sari",
      "username": "ibusari",
      "type": "private"
    },
    "date": 1707235200,
    "text": "/start",
    "entities": [
      {
        "offset": 0,
        "length": 6,
        "type": "bot_command"
      }
    ]
  }
}
```

### Install Telegram.Bot Package

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Official Telegram Bot SDK for .NET
dotnet add package Telegram.Bot
```

### Telegram Update Models

Create `src/Nastart.Api/Features/Bot/TelegramModels.cs`:

```csharp
using System.Text.Json.Serialization;

namespace Nastart.Api.Features.Bot;

/// <summary>
/// Represents a Telegram update (webhook payload).
/// </summary>
/// <remarks>
/// Simplified models for our use case.
/// Full models available in Telegram.Bot package.
/// See: https://core.telegram.org/bots/api#update
/// </remarks>
public sealed record TelegramUpdate
{
    [JsonPropertyName("update_id")]
    public long UpdateId { get; init; }
    
    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; init; }
    
    [JsonPropertyName("callback_query")]
    public CallbackQuery? CallbackQuery { get; init; }
    
    [JsonPropertyName("edited_message")]
    public TelegramMessage? EditedMessage { get; init; }
}

/// <summary>
/// Represents a Telegram message.
/// </summary>
public sealed record TelegramMessage
{
    [JsonPropertyName("message_id")]
    public int MessageId { get; init; }
    
    [JsonPropertyName("from")]
    public TelegramUser? From { get; init; }
    
    [JsonPropertyName("chat")]
    public TelegramChat Chat { get; init; } = null!;
    
    [JsonPropertyName("date")]
    public long Date { get; init; }
    
    [JsonPropertyName("text")]
    public string? Text { get; init; }
    
    [JsonPropertyName("photo")]
    public PhotoSize[]? Photo { get; init; }
    
    [JsonPropertyName("document")]
    public Document? Document { get; init; }
    
    [JsonPropertyName("caption")]
    public string? Caption { get; init; }
    
    [JsonPropertyName("entities")]
    public MessageEntity[]? Entities { get; init; }
    
    /// <summary>
    /// Checks if message is a bot command.
    /// </summary>
    public bool IsCommand => Text?.StartsWith('/') == true;
    
    /// <summary>
    /// Gets the command name (without /).
    /// </summary>
    public string? CommandName => IsCommand 
        ? Text?.Split(' ', '@')[0].TrimStart('/').ToLowerInvariant()
        : null;
    
    /// <summary>
    /// Gets command arguments (text after command).
    /// </summary>
    public string? CommandArgs => IsCommand && Text?.Contains(' ') == true
        ? Text[(Text.IndexOf(' ') + 1)..]
        : null;
}

/// <summary>
/// Represents a Telegram user.
/// </summary>
public sealed record TelegramUser
{
    [JsonPropertyName("id")]
    public long Id { get; init; }
    
    [JsonPropertyName("is_bot")]
    public bool IsBot { get; init; }
    
    [JsonPropertyName("first_name")]
    public string FirstName { get; init; } = "";
    
    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }
    
    [JsonPropertyName("username")]
    public string? Username { get; init; }
    
    [JsonPropertyName("language_code")]
    public string? LanguageCode { get; init; }
    
    /// <summary>
    /// Gets full name (first + last).
    /// </summary>
    public string FullName => string.IsNullOrEmpty(LastName) 
        ? FirstName 
        : $"{FirstName} {LastName}";
}

/// <summary>
/// Represents a Telegram chat.
/// </summary>
public sealed record TelegramChat
{
    [JsonPropertyName("id")]
    public long Id { get; init; }
    
    [JsonPropertyName("type")]
    public string Type { get; init; } = "private";
    
    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }
    
    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }
    
    [JsonPropertyName("username")]
    public string? Username { get; init; }
}

/// <summary>
/// Represents a photo size variant.
/// </summary>
public sealed record PhotoSize
{
    [JsonPropertyName("file_id")]
    public string FileId { get; init; } = "";
    
    [JsonPropertyName("file_unique_id")]
    public string FileUniqueId { get; init; } = "";
    
    [JsonPropertyName("width")]
    public int Width { get; init; }
    
    [JsonPropertyName("height")]
    public int Height { get; init; }
    
    [JsonPropertyName("file_size")]
    public int? FileSize { get; init; }
}

/// <summary>
/// Represents a document/file.
/// </summary>
public sealed record Document
{
    [JsonPropertyName("file_id")]
    public string FileId { get; init; } = "";
    
    [JsonPropertyName("file_unique_id")]
    public string FileUniqueId { get; init; } = "";
    
    [JsonPropertyName("file_name")]
    public string? FileName { get; init; }
    
    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }
    
    [JsonPropertyName("file_size")]
    public int? FileSize { get; init; }
}

/// <summary>
/// Represents a message entity (command, mention, URL, etc).
/// </summary>
public sealed record MessageEntity
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";
    
    [JsonPropertyName("offset")]
    public int Offset { get; init; }
    
    [JsonPropertyName("length")]
    public int Length { get; init; }
}

/// <summary>
/// Represents a callback query (button click).
/// </summary>
public sealed record CallbackQuery
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
    
    [JsonPropertyName("from")]
    public TelegramUser From { get; init; } = null!;
    
    [JsonPropertyName("message")]
    public TelegramMessage? Message { get; init; }
    
    [JsonPropertyName("data")]
    public string? Data { get; init; }
}
```

### Your Task (Day 2):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Install Telegram.Bot package
dotnet add package Telegram.Bot

# Create models file
New-Item Features\Bot\TelegramModels.cs

# Verify build
dotnet build
```

---

# Day 3: TelegramWebhookEndpoint Feature

## 🧒 Explain Like I'm 5

Now we need to build a "mailbox" 📬 where Telegram delivers messages!

When someone sends a message to your bot:
1. Telegram knocks on your mailbox (webhook endpoint)
2. You open the mailbox and see what's inside
3. You figure out what to do with the message
4. You send a reply back!

## 🔧 Engineer Language

The **TelegramWebhookEndpoint** feature handles incoming webhook requests from Telegram. It validates the request, deserializes the update, and dispatches it to the appropriate handler.

> 📖 **Microsoft Docs**: *"Minimal APIs are ideal for handling webhooks. Define a simple POST endpoint that receives and processes the webhook payload."*
>
> — [Minimal APIs overview](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview)

### Telegram Bot Service

Create `src/Nastart.Api/Features/Bot/TelegramBotService.cs`:

```csharp
using Microsoft.Extensions.Options;
using Nastart.Api.Shared.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Nastart.Api.Features.Bot;

/// <summary>
/// Service for interacting with Telegram Bot API.
/// </summary>
/// <remarks>
/// Wraps Telegram.Bot client with simplified methods.
/// See: https://core.telegram.org/bots/api
/// </remarks>
public interface ITelegramBotService
{
    /// <summary>
    /// Sends a text message to a chat.
    /// </summary>
    Task<Message> SendTextMessageAsync(
        long chatId,
        string text,
        IReplyMarkup? replyMarkup = null,
        ParseMode parseMode = ParseMode.Html,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Answers a callback query (acknowledges button click).
    /// </summary>
    Task AnswerCallbackQueryAsync(
        string callbackQueryId,
        string? text = null,
        bool showAlert = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Edits an existing message.
    /// </summary>
    Task<Message> EditMessageTextAsync(
        long chatId,
        int messageId,
        string text,
        IReplyMarkup? replyMarkup = null,
        ParseMode parseMode = ParseMode.Html,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sends a "typing..." indicator.
    /// </summary>
    Task SendTypingActionAsync(
        long chatId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets file download URL.
    /// </summary>
    Task<string> GetFileUrlAsync(
        string fileId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Downloads a file as byte array.
    /// </summary>
    Task<byte[]> DownloadFileAsync(
        string fileId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets up the webhook.
    /// </summary>
    Task SetWebhookAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of Telegram bot service.
/// </summary>
public class TelegramBotService : ITelegramBotService
{
    private readonly ITelegramBotClient _botClient;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly HttpClient _httpClient;

    public TelegramBotService(
        ITelegramBotClient botClient,
        IOptions<TelegramOptions> options,
        ILogger<TelegramBotService> logger,
        HttpClient httpClient)
    {
        _botClient = botClient;
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<Message> SendTextMessageAsync(
        long chatId,
        string text,
        IReplyMarkup? replyMarkup = null,
        ParseMode parseMode = ParseMode.Html,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending message to chat {ChatId}: {Text}",
            chatId,
            text.Length > 50 ? text[..50] + "..." : text);
        
        return await _botClient.SendMessage(
            chatId: chatId,
            text: text,
            parseMode: parseMode,
            replyMarkup: replyMarkup,
            cancellationToken: cancellationToken);
    }

    public async Task AnswerCallbackQueryAsync(
        string callbackQueryId,
        string? text = null,
        bool showAlert = false,
        CancellationToken cancellationToken = default)
    {
        await _botClient.AnswerCallbackQuery(
            callbackQueryId: callbackQueryId,
            text: text,
            showAlert: showAlert,
            cancellationToken: cancellationToken);
    }

    public async Task<Message> EditMessageTextAsync(
        long chatId,
        int messageId,
        string text,
        IReplyMarkup? replyMarkup = null,
        ParseMode parseMode = ParseMode.Html,
        CancellationToken cancellationToken = default)
    {
        return await _botClient.EditMessageText(
            chatId: chatId,
            messageId: messageId,
            text: text,
            parseMode: parseMode,
            replyMarkup: replyMarkup as InlineKeyboardMarkup,
            cancellationToken: cancellationToken);
    }

    public async Task SendTypingActionAsync(
        long chatId,
        CancellationToken cancellationToken = default)
    {
        await _botClient.SendChatAction(
            chatId: chatId,
            chatAction: ChatAction.Typing,
            cancellationToken: cancellationToken);
    }

    public async Task<string> GetFileUrlAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        var file = await _botClient.GetFile(fileId, cancellationToken);
        return $"https://api.telegram.org/file/bot{_options.BotToken}/{file.FilePath}";
    }

    public async Task<byte[]> DownloadFileAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        var fileUrl = await GetFileUrlAsync(fileId, cancellationToken);
        return await _httpClient.GetByteArrayAsync(fileUrl, cancellationToken);
    }

    public async Task SetWebhookAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Setting webhook to {WebhookUrl}",
            _options.WebhookUrl);
        
        await _botClient.SetWebhook(
            url: _options.WebhookUrl,
            secretToken: _options.SecretToken,
            allowedUpdates: _options.AllowedUpdates
                .Select(Enum.Parse<UpdateType>)
                .ToArray(),
            cancellationToken: cancellationToken);
        
        _logger.LogInformation("Webhook set successfully");
    }
}
```

### Webhook Endpoint Feature

Create `src/Nastart.Api/Features/Bot/HandleWebhook.cs`:

```csharp
using System.Text.Json;
using MediatR;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Features.Bot;

// ══════════════════════════════════════════════════════════════
// HANDLE WEBHOOK FEATURE SLICE
// Entry point for all Telegram updates
// ══════════════════════════════════════════════════════════════

// ── Request ──
/// <summary>
/// Command to process incoming Telegram webhook.
/// </summary>
public sealed record HandleWebhookCommand(
    TelegramUpdate Update
) : IRequest<Result<WebhookResponse>>;

// ── Response ──
/// <summary>
/// Response from webhook processing.
/// </summary>
public sealed record WebhookResponse(
    bool Processed,
    string? HandlerUsed,
    string? ErrorMessage = null
);

// ── Handler ──
/// <summary>
/// Handles incoming Telegram webhooks by routing to appropriate command handlers.
/// </summary>
/// <remarks>
/// Acts as a dispatcher — routes updates to specific feature handlers.
/// See: https://core.telegram.org/bots/api#getting-updates
/// </remarks>
public sealed class HandleWebhookHandler 
    : IRequestHandler<HandleWebhookCommand, Result<WebhookResponse>>
{
    private readonly IMediator _mediator;
    private readonly ILogger<HandleWebhookHandler> _logger;

    public HandleWebhookHandler(
        IMediator mediator,
        ILogger<HandleWebhookHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<WebhookResponse>> Handle(
        HandleWebhookCommand request,
        CancellationToken cancellationToken)
    {
        var update = request.Update;
        
        _logger.LogInformation(
            "Processing update {UpdateId}",
            update.UpdateId);
        
        try
        {
            // Route based on update type
            string? handlerUsed = null;
            
            if (update.Message is not null)
            {
                handlerUsed = await HandleMessage(update.Message, cancellationToken);
            }
            else if (update.CallbackQuery is not null)
            {
                handlerUsed = await HandleCallbackQuery(update.CallbackQuery, cancellationToken);
            }
            else if (update.EditedMessage is not null)
            {
                // Optionally handle edited messages
                handlerUsed = "EditedMessageIgnored";
            }
            else
            {
                _logger.LogWarning(
                    "Unknown update type for update {UpdateId}",
                    update.UpdateId);
                handlerUsed = "UnknownType";
            }
            
            return Result<WebhookResponse>.Success(new WebhookResponse(
                Processed: true,
                HandlerUsed: handlerUsed
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error processing update {UpdateId}",
                update.UpdateId);
            
            return Result<WebhookResponse>.Success(new WebhookResponse(
                Processed: false,
                HandlerUsed: null,
                ErrorMessage: ex.Message
            ));
        }
    }

    private async Task<string> HandleMessage(
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        // Check for photos (receipt scanning)
        if (message.Photo is { Length: > 0 })
        {
            // TODO: Dispatch to ScanReceipt handler (Week 9)
            _logger.LogInformation(
                "Photo received from chat {ChatId}",
                message.Chat.Id);
            return "PhotoReceived";
        }
        
        // Check for commands
        if (message.IsCommand)
        {
            return await DispatchCommand(message, cancellationToken);
        }
        
        // Regular text message
        if (!string.IsNullOrEmpty(message.Text))
        {
            // TODO: Handle conversational state
            _logger.LogInformation(
                "Text received: {Text}",
                message.Text);
            return "TextMessage";
        }
        
        return "UnhandledMessage";
    }

    private async Task<string> DispatchCommand(
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        var commandName = message.CommandName;
        
        _logger.LogInformation(
            "Command received: /{Command} from chat {ChatId}",
            commandName,
            message.Chat.Id);
        
        return commandName switch
        {
            "start" => await DispatchStartCommand(message, cancellationToken),
            "help" => await DispatchHelpCommand(message, cancellationToken),
            // TODO: Add more commands in Week 10
            // "cost" => await DispatchCostCommand(message, cancellationToken),
            // "price" => await DispatchPriceCommand(message, cancellationToken),
            // "low" => await DispatchLowStockCommand(message, cancellationToken),
            _ => await HandleUnknownCommand(message, cancellationToken)
        };
    }

    private async Task<string> DispatchStartCommand(
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new StartCommand(
            ChatId: message.Chat.Id,
            User: message.From
        ), cancellationToken);
        
        return "StartCommand";
    }

    private async Task<string> DispatchHelpCommand(
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new HelpCommand(
            ChatId: message.Chat.Id
        ), cancellationToken);
        
        return "HelpCommand";
    }

    private async Task<string> HandleUnknownCommand(
        TelegramMessage message,
        CancellationToken cancellationToken)
    {
        // Send help message for unknown commands
        await _mediator.Send(new HelpCommand(
            ChatId: message.Chat.Id
        ), cancellationToken);
        
        return "UnknownCommand";
    }

    private async Task<string> HandleCallbackQuery(
        CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Callback query received: {Data}",
            callbackQuery.Data);
        
        // TODO: Dispatch to appropriate callback handler
        // Based on callbackQuery.Data pattern
        
        return "CallbackQuery";
    }
}
```

### Bot Endpoints

Create `src/Nastart.Api/Features/Bot/BotEndpoints.cs`:

```csharp
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Nastart.Api.Shared.Configuration;

namespace Nastart.Api.Features.Bot;

/// <summary>
/// Endpoint mappings for Telegram bot webhook.
/// </summary>
public static class BotEndpoints
{
    public static void MapBotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bot")
            .WithTags("Bot")
            .WithOpenApi();
        
        // Webhook endpoint (receives updates from Telegram)
        group.MapPost("/webhook", HandleWebhook)
            .WithName("TelegramWebhook")
            .WithSummary("Telegram webhook endpoint")
            .WithDescription("Receives updates from Telegram Bot API. Should not be called directly.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
        
        // Setup webhook (call once to register with Telegram)
        group.MapPost("/setup-webhook", SetupWebhook)
            .WithName("SetupWebhook")
            .WithSummary("Register webhook with Telegram")
            .WithDescription("Registers the webhook URL with Telegram. Call this after deployment.")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status500InternalServerError);
        
        // Health check for bot
        group.MapGet("/status", GetBotStatus)
            .WithName("BotStatus")
            .WithSummary("Get bot status")
            .Produces<BotStatusResponse>(StatusCodes.Status200OK);
    }
    
    /// <summary>
    /// Handles incoming Telegram webhook.
    /// </summary>
    private static async Task<IResult> HandleWebhook(
        [FromBody] TelegramUpdate update,
        [FromHeader(Name = "X-Telegram-Bot-Api-Secret-Token")] string? secretToken,
        [FromServices] IMediator mediator,
        [FromServices] IOptions<TelegramOptions> options,
        [FromServices] ILogger<TelegramUpdate> logger)
    {
        // Validate secret token
        if (!string.IsNullOrEmpty(options.Value.SecretToken))
        {
            if (secretToken != options.Value.SecretToken)
            {
                logger.LogWarning(
                    "Invalid secret token received for update {UpdateId}",
                    update.UpdateId);
                return Results.Unauthorized();
            }
        }
        
        // Process the update
        var result = await mediator.Send(new HandleWebhookCommand(update));
        
        // Always return 200 to Telegram (don't retry failed updates)
        return Results.Ok(new { processed = result.IsSuccess });
    }
    
    /// <summary>
    /// Sets up the webhook with Telegram.
    /// </summary>
    private static async Task<IResult> SetupWebhook(
        [FromServices] ITelegramBotService botService,
        [FromServices] ILogger<ITelegramBotService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await botService.SetWebhookAsync(cancellationToken);
            return Results.Ok(new { success = true, message = "Webhook registered successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to setup webhook");
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
    
    /// <summary>
    /// Gets bot status information.
    /// </summary>
    private static async Task<IResult> GetBotStatus(
        [FromServices] IOptions<TelegramOptions> options)
    {
        return Results.Ok(new BotStatusResponse(
            Status: "running",
            WebhookConfigured: !string.IsNullOrEmpty(options.Value.WebhookUrl),
            AllowedUpdates: options.Value.AllowedUpdates
        ));
    }
}

/// <summary>
/// Bot status response.
/// </summary>
public sealed record BotStatusResponse(
    string Status,
    bool WebhookConfigured,
    string[] AllowedUpdates
);
```

### Register Services in Program.cs

```csharp
using Nastart.Api.Features.Bot;
using Nastart.Api.Shared.Configuration;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

// ══════════════════════════════════════════════════════════════
// TELEGRAM BOT CONFIGURATION
// ══════════════════════════════════════════════════════════════

builder.Services.Configure<TelegramOptions>(
    builder.Configuration.GetSection(TelegramOptions.SectionName));

// Register Telegram Bot Client
builder.Services.AddHttpClient("telegram")
    .AddTypedClient<ITelegramBotClient>((httpClient, sp) =>
    {
        var options = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
        return new TelegramBotClient(options.BotToken, httpClient);
    });

// Register bot service
builder.Services.AddScoped<ITelegramBotService, TelegramBotService>();

var app = builder.Build();

// Map bot endpoints
app.MapBotEndpoints();
```

### Your Task (Day 3):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create the service and endpoint files
New-Item Features\Bot\TelegramBotService.cs
New-Item Features\Bot\HandleWebhook.cs
New-Item Features\Bot\BotEndpoints.cs

# Verify build
dotnet build
```

---

# Day 4: StartCommand Feature Slice

## 🧒 Explain Like I'm 5

When someone meets your robot for the first time, it should say "Hello!" 👋 and introduce itself!

The `/start` command is like a friendly greeting:
- "Welcome to Nastart! 🍰"
- "I help bakers track ingredients and calculate costs!"
- "Here's what I can do..."
- "Send me a receipt photo to get started!"

## 🔧 Engineer Language

The **StartCommand** feature handles the `/start` command — the entry point for new users. It welcomes users, explains the bot's capabilities, and optionally creates or links user accounts.

> 📖 **Telegram Docs**: *"The /start command is special. When a user first opens a conversation with your bot, Telegram automatically sends this command."*
>
> — [Telegram Bot Commands](https://core.telegram.org/bots/features#commands)

### Complete StartCommand Feature

Create `src/Nastart.Api/Features/Bot/Commands/StartCommand.cs`:

```csharp
using MediatR;
using Microsoft.EntityFrameworkCore;
using Nastart.Api.Shared.Data;
using Telegram.Bot.Types.ReplyMarkups;

namespace Nastart.Api.Features.Bot;

// ══════════════════════════════════════════════════════════════
// START COMMAND FEATURE SLICE
// Handles /start command — welcome message and onboarding
// ══════════════════════════════════════════════════════════════

// ── Request ──
/// <summary>
/// Command to handle /start bot command.
/// </summary>
public sealed record StartCommand(
    long ChatId,
    TelegramUser? User
) : IRequest<Unit>;

// ── Handler ──
/// <summary>
/// Handles the /start command by sending welcome message.
/// </summary>
/// <remarks>
/// Creates or links user account if needed.
/// See: https://core.telegram.org/bots/features#commands
/// </remarks>
public sealed class StartCommandHandler : IRequestHandler<StartCommand, Unit>
{
    private readonly ITelegramBotService _botService;
    private readonly NastartDbContext _db;
    private readonly ILogger<StartCommandHandler> _logger;

    public StartCommandHandler(
        ITelegramBotService botService,
        NastartDbContext db,
        ILogger<StartCommandHandler> logger)
    {
        _botService = botService;
        _db = db;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        StartCommand request,
        CancellationToken cancellationToken)
    {
        var chatId = request.ChatId;
        var user = request.User;
        
        _logger.LogInformation(
            "Start command from chat {ChatId}, user {UserId}",
            chatId,
            user?.Id);
        
        // Check if user exists
        var existingUser = user is not null
            ? await _db.Users.FirstOrDefaultAsync(
                u => u.TelegramId == user.Id,
                cancellationToken)
            : null;
        
        // Build welcome message
        var welcomeMessage = BuildWelcomeMessage(user, existingUser is not null);
        
        // Build reply keyboard
        var keyboard = BuildQuickStartKeyboard(existingUser is not null);
        
        // Send welcome message
        await _botService.SendTextMessageAsync(
            chatId,
            welcomeMessage,
            keyboard,
            cancellationToken: cancellationToken);
        
        // If new user, prompt for registration
        if (existingUser is null && user is not null)
        {
            await SendRegistrationPrompt(chatId, user, cancellationToken);
        }
        
        return Unit.Value;
    }

    private static string BuildWelcomeMessage(TelegramUser? user, bool isExistingUser)
    {
        var greeting = user is not null
            ? $"Halo, <b>{user.FirstName}</b>! 👋"
            : "Halo! 👋";
        
        if (isExistingUser)
        {
            return $"""
                {greeting}
                
                Selamat datang kembali di <b>Nastart</b>! 🍰
                
                Saya siap membantu Anda:
                📸 Scan struk belanja → ketik harga bahan otomatis
                💰 Hitung biaya resep → tahu untung rugi
                📊 Pantau harga bahan → dapat alert kalau naik
                
                <i>Kirim foto struk untuk mulai, atau ketik /help untuk bantuan.</i>
                """;
        }
        
        return $"""
            {greeting}
            
            Selamat datang di <b>Nastart</b>! 🍰
            
            Saya adalah asisten pintar untuk pemilik usaha bakery & F&B.
            
            <b>Apa yang bisa saya bantu?</b>
            📸 <b>Scan Struk</b> — Foto struk belanja, saya input harga bahan otomatis
            💰 <b>Hitung Biaya Resep</b> — Tahu persis biaya produksi per resep
            📊 <b>Pantau Harga</b> — Dapat notifikasi kalau harga bahan naik drastis
            ⚠️ <b>Alert Stok</b> — Diingatkan kalau stok bahan menipis
            
            <b>Mulai sekarang:</b>
            1️⃣ Kirim foto struk belanja Anda
            2️⃣ Konfirmasi bahan yang terdeteksi
            3️⃣ Lihat update harga & margin resep!
            
            <i>Ketik /help untuk daftar perintah lengkap.</i>
            """;
    }

    private static InlineKeyboardMarkup BuildQuickStartKeyboard(bool isExistingUser)
    {
        if (isExistingUser)
        {
            return new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📸 Scan Struk", "action:scan"),
                    InlineKeyboardButton.WithCallbackData("💰 Cek Resep", "action:recipes"),
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📊 Dashboard", "action:dashboard"),
                    InlineKeyboardButton.WithCallbackData("❓ Bantuan", "action:help"),
                }
            });
        }
        
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🚀 Mulai Sekarang", "onboard:start"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📖 Pelajari Dulu", "onboard:learn"),
                InlineKeyboardButton.WithCallbackData("❓ Bantuan", "action:help"),
            }
        });
    }

    private async Task SendRegistrationPrompt(
        long chatId,
        TelegramUser user,
        CancellationToken cancellationToken)
    {
        // TODO: Implement full registration flow
        // For now, just create a basic user record
        
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            TelegramId = user.Id,
            TelegramUsername = user.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            LanguageCode = user.LanguageCode ?? "id",
            MinimumMargin = 30m, // Default 30% margin
            CreatedAt = DateTime.UtcNow
        };
        
        _db.Users.Add(newUser);
        await _db.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation(
            "Created new user {UserId} for Telegram user {TelegramId}",
            newUser.Id,
            user.Id);
    }
}
```

### User Entity (if not exists)

Create `src/Nastart.Api/Features/Users/User.cs`:

```csharp
namespace Nastart.Api.Features.Users;

/// <summary>
/// Represents a Nastart user account.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Telegram user ID (unique).
    /// </summary>
    public long? TelegramId { get; set; }
    
    /// <summary>
    /// Telegram username (optional).
    /// </summary>
    public string? TelegramUsername { get; set; }
    
    /// <summary>
    /// User's first name.
    /// </summary>
    public required string FirstName { get; set; }
    
    /// <summary>
    /// User's last name.
    /// </summary>
    public string? LastName { get; set; }
    
    /// <summary>
    /// User's email (for web login).
    /// </summary>
    public string? Email { get; set; }
    
    /// <summary>
    /// User's phone number.
    /// </summary>
    public string? PhoneNumber { get; set; }
    
    /// <summary>
    /// Preferred language code (default: "id").
    /// </summary>
    public string LanguageCode { get; set; } = "id";
    
    /// <summary>
    /// Minimum acceptable margin percentage for alerts.
    /// </summary>
    public decimal MinimumMargin { get; set; } = 30m;
    
    /// <summary>
    /// When the user was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the user was last active.
    /// </summary>
    public DateTime? LastActiveAt { get; set; }
    
    /// <summary>
    /// Whether the account is active.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
```

### Add User DbSet to Context

```csharp
// In NastartDbContext.cs, add:
using Nastart.Api.Features.Users;

public DbSet<User> Users => Set<User>();
```

### Your Task (Day 4):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create Commands folder
mkdir Features\Bot\Commands -ErrorAction SilentlyContinue

# Create User entity
mkdir Features\Users -ErrorAction SilentlyContinue
New-Item Features\Users\User.cs

# Create StartCommand feature
New-Item Features\Bot\Commands\StartCommand.cs

# Update DbContext with Users DbSet

# Verify build
dotnet build
```

---

# Day 5: HelpCommand Feature Slice

## 🧒 Explain Like I'm 5

When someone is confused and asks "Help!", your robot should show them all the things it can do! Like a helpful menu:

- "📸 Send a photo of your receipt to scan it"
- "⌨️ /cost - Calculate recipe cost"
- "⌨️ /price - Check ingredient price"
- "⌨️ /low - See low stock items"

## 🔧 Engineer Language

The **HelpCommand** feature displays available commands and usage instructions. It's crucial for user experience — users should always know what they can do.

> 📖 **Telegram Docs**: *"Provide a /help command so users can easily discover your bot's features at any time."*
>
> — [Telegram Bot Best Practices](https://core.telegram.org/bots/features#commands)

### Complete HelpCommand Feature

Create `src/Nastart.Api/Features/Bot/Commands/HelpCommand.cs`:

```csharp
using MediatR;
using Telegram.Bot.Types.ReplyMarkups;

namespace Nastart.Api.Features.Bot;

// ══════════════════════════════════════════════════════════════
// HELP COMMAND FEATURE SLICE
// Handles /help command — shows available commands
// ══════════════════════════════════════════════════════════════

// ── Request ──
/// <summary>
/// Command to handle /help bot command.
/// </summary>
public sealed record HelpCommand(
    long ChatId
) : IRequest<Unit>;

// ── Handler ──
/// <summary>
/// Handles the /help command by showing available commands.
/// </summary>
public sealed class HelpCommandHandler : IRequestHandler<HelpCommand, Unit>
{
    private readonly ITelegramBotService _botService;
    private readonly ILogger<HelpCommandHandler> _logger;

    public HelpCommandHandler(
        ITelegramBotService botService,
        ILogger<HelpCommandHandler> logger)
    {
        _botService = botService;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        HelpCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Help command from chat {ChatId}",
            request.ChatId);
        
        var helpMessage = BuildHelpMessage();
        var keyboard = BuildHelpKeyboard();
        
        await _botService.SendTextMessageAsync(
            request.ChatId,
            helpMessage,
            keyboard,
            cancellationToken: cancellationToken);
        
        return Unit.Value;
    }

    private static string BuildHelpMessage()
    {
        return """
            <b>📖 Panduan Nastart Bot</b>
            
            <b>📸 SCAN STRUK</b>
            Kirim foto struk belanja, saya akan:
            • Baca teks dengan OCR
            • Deteksi bahan & harga
            • Update stok & harga otomatis
            
            <b>⌨️ PERINTAH</b>
            /start — Mulai dari awal
            /help — Tampilkan bantuan ini
            /cost <i>[nama resep]</i> — Hitung biaya resep
            /price <i>[nama bahan]</i> — Cek harga bahan
            /low — Lihat bahan stok menipis
            /profit — Ringkasan profit
            
            <b>💡 TIPS</b>
            • Foto struk harus jelas & tidak blur
            • Pastikan semua harga terlihat
            • Konfirmasi hasil scan sebelum disimpan
            
            <b>🔔 NOTIFIKASI</b>
            Bot akan memberi tahu jika:
            • Harga bahan naik >15%
            • Margin resep turun di bawah target
            • Stok bahan menipis
            
            <i>Ada pertanyaan? Hubungi @nastart_support</i>
            """;
    }

    private static InlineKeyboardMarkup BuildHelpKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📸 Cara Scan Struk", "help:scan"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("💰 Cara Hitung Resep", "help:recipe"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📊 Fitur Lainnya", "help:features"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 Kembali", "action:back"),
            }
        });
    }
}
```

### Help Callback Handlers

Create `src/Nastart.Api/Features/Bot/Callbacks/HelpCallbacks.cs`:

```csharp
using MediatR;
using Telegram.Bot.Types.ReplyMarkups;

namespace Nastart.Api.Features.Bot;

// ══════════════════════════════════════════════════════════════
// HELP CALLBACKS FEATURE SLICE
// Handles help-related button clicks
// ══════════════════════════════════════════════════════════════

// ── Help Scan Callback ──
public sealed record HelpScanCallback(
    long ChatId,
    int MessageId,
    string CallbackQueryId
) : IRequest<Unit>;

public sealed class HelpScanCallbackHandler : IRequestHandler<HelpScanCallback, Unit>
{
    private readonly ITelegramBotService _botService;

    public HelpScanCallbackHandler(ITelegramBotService botService)
    {
        _botService = botService;
    }

    public async Task<Unit> Handle(
        HelpScanCallback request,
        CancellationToken cancellationToken)
    {
        // Answer callback first
        await _botService.AnswerCallbackQueryAsync(
            request.CallbackQueryId,
            cancellationToken: cancellationToken);
        
        var message = """
            <b>📸 Cara Scan Struk</b>
            
            <b>Langkah 1: Persiapkan Struk</b>
            • Letakkan struk di permukaan rata
            • Pastikan pencahayaan cukup
            • Hindari lipatan atau sobek
            
            <b>Langkah 2: Ambil Foto</b>
            • Foto seluruh struk dari atas
            • Pastikan semua teks terbaca jelas
            • Hindari blur atau bayangan
            
            <b>Langkah 3: Kirim ke Bot</b>
            • Kirim foto langsung ke chat ini
            • Tunggu proses OCR (5-10 detik)
            • Review hasil deteksi
            
            <b>Langkah 4: Konfirmasi</b>
            • Periksa bahan yang terdeteksi
            • Koreksi jika ada kesalahan
            • Tekan "Simpan" untuk update stok
            
            <i>💡 Tip: Foto close-up bagian harga jika hasil kurang akurat</i>
            """;
        
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 Kembali ke Bantuan", "action:help"),
            }
        });
        
        await _botService.EditMessageTextAsync(
            request.ChatId,
            request.MessageId,
            message,
            keyboard,
            cancellationToken: cancellationToken);
        
        return Unit.Value;
    }
}

// ── Help Recipe Callback ──
public sealed record HelpRecipeCallback(
    long ChatId,
    int MessageId,
    string CallbackQueryId
) : IRequest<Unit>;

public sealed class HelpRecipeCallbackHandler : IRequestHandler<HelpRecipeCallback, Unit>
{
    private readonly ITelegramBotService _botService;

    public HelpRecipeCallbackHandler(ITelegramBotService botService)
    {
        _botService = botService;
    }

    public async Task<Unit> Handle(
        HelpRecipeCallback request,
        CancellationToken cancellationToken)
    {
        await _botService.AnswerCallbackQueryAsync(
            request.CallbackQueryId,
            cancellationToken: cancellationToken);
        
        var message = """
            <b>💰 Cara Hitung Biaya Resep</b>
            
            <b>Perintah:</b>
            <code>/cost [nama resep]</code>
            
            <b>Contoh:</b>
            <code>/cost Nastar</code>
            <code>/cost Brownies Coklat</code>
            
            <b>Hasil yang ditampilkan:</b>
            • Total biaya bahan per resep
            • Biaya per item/porsi
            • Margin profit berdasarkan harga jual
            • Peringatan jika margin terlalu rendah
            
            <b>Tips:</b>
            • Pastikan semua bahan sudah diinput
            • Update harga bahan secara rutin
            • Set target margin di pengaturan
            
            <i>💡 Harga bahan diambil dari data pembelian terakhir</i>
            """;
        
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🔙 Kembali ke Bantuan", "action:help"),
            }
        });
        
        await _botService.EditMessageTextAsync(
            request.ChatId,
            request.MessageId,
            message,
            keyboard,
            cancellationToken: cancellationToken);
        
        return Unit.Value;
    }
}
```

### Your Task (Day 5):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create Callbacks folder
mkdir Features\Bot\Callbacks -ErrorAction SilentlyContinue

# Create help command and callbacks
New-Item Features\Bot\Commands\HelpCommand.cs
New-Item Features\Bot\Callbacks\HelpCallbacks.cs

# Verify build
dotnet build
```

---

# Day 6: Command Routing & Dispatcher

## 🧒 Explain Like I'm 5

Imagine your robot gets lots of different messages:
- "Hi!" → Just chatting
- "/help" → Asking for help
- "📸 Photo" → Scanning a receipt
- "Button clicked" → Chose an option

Your robot needs a **traffic cop** 🚦 that says:
- "This message goes to the HELP handler!"
- "This photo goes to the SCAN handler!"
- "This button click goes to the CONFIRM handler!"

## 🔧 Engineer Language

**Command routing** dispatches incoming updates to the appropriate feature handlers based on update type and content. We extend the webhook handler with proper callback routing.

> 📖 **Microsoft Docs**: *"MediatR provides a simple, unambitious mediator implementation in .NET. It supports request/response, commands, queries, notifications, and events."*
>
> — [MediatR on GitHub](https://github.com/jbogard/MediatR)

### Update Webhook Handler with Callback Routing

Update `src/Nastart.Api/Features/Bot/HandleWebhook.cs`:

```csharp
// Add to HandleWebhookHandler class:

private async Task<string> HandleCallbackQuery(
    CallbackQuery callbackQuery,
    CancellationToken cancellationToken)
{
    var data = callbackQuery.Data ?? "";
    var chatId = callbackQuery.Message?.Chat.Id ?? 0;
    var messageId = callbackQuery.Message?.MessageId ?? 0;
    
    _logger.LogInformation(
        "Callback query: {Data} from chat {ChatId}",
        data,
        chatId);
    
    // Parse callback data pattern: "category:action" or "category:action:param"
    var parts = data.Split(':');
    var category = parts.Length > 0 ? parts[0] : "";
    var action = parts.Length > 1 ? parts[1] : "";
    var param = parts.Length > 2 ? parts[2] : null;
    
    return (category, action) switch
    {
        // Help callbacks
        ("help", "scan") => await DispatchCallback<HelpScanCallback>(
            new HelpScanCallback(chatId, messageId, callbackQuery.Id), cancellationToken),
        ("help", "recipe") => await DispatchCallback<HelpRecipeCallback>(
            new HelpRecipeCallback(chatId, messageId, callbackQuery.Id), cancellationToken),
        
        // Action callbacks
        ("action", "help") => await DispatchHelpFromCallback(
            chatId, messageId, callbackQuery.Id, cancellationToken),
        ("action", "back") => await DispatchBackAction(
            chatId, messageId, callbackQuery.Id, cancellationToken),
        ("action", "scan") => await DispatchScanPrompt(
            chatId, messageId, callbackQuery.Id, cancellationToken),
        
        // Onboarding callbacks
        ("onboard", "start") => await DispatchOnboardStart(
            chatId, messageId, callbackQuery.Id, cancellationToken),
        ("onboard", "learn") => await DispatchOnboardLearn(
            chatId, messageId, callbackQuery.Id, cancellationToken),
        
        // Confirm/reject callbacks (for receipt scanning)
        ("confirm", _) => await DispatchConfirmAction(
            chatId, messageId, callbackQuery.Id, action, param, cancellationToken),
        
        // Unknown callback
        _ => await HandleUnknownCallback(callbackQuery.Id, cancellationToken)
    };
}

private async Task<string> DispatchCallback<T>(
    T command,
    CancellationToken cancellationToken) where T : IRequest<Unit>
{
    await _mediator.Send(command, cancellationToken);
    return typeof(T).Name;
}

private async Task<string> DispatchHelpFromCallback(
    long chatId,
    int messageId,
    string callbackQueryId,
    CancellationToken cancellationToken)
{
    // Answer callback
    await _mediator.Send(new AnswerCallbackCommand(callbackQueryId), cancellationToken);
    
    // Send help message
    await _mediator.Send(new HelpCommand(chatId), cancellationToken);
    
    return "HelpFromCallback";
}

private async Task<string> DispatchBackAction(
    long chatId,
    int messageId,
    string callbackQueryId,
    CancellationToken cancellationToken)
{
    // Answer callback and go back to start
    await _mediator.Send(new AnswerCallbackCommand(callbackQueryId), cancellationToken);
    await _mediator.Send(new StartCommand(chatId, null), cancellationToken);
    return "BackAction";
}

private async Task<string> DispatchScanPrompt(
    long chatId,
    int messageId,
    string callbackQueryId,
    CancellationToken cancellationToken)
{
    // TODO: Send scan prompt message
    await _mediator.Send(new AnswerCallbackCommand(
        callbackQueryId, 
        "Kirim foto struk untuk di-scan"), cancellationToken);
    return "ScanPrompt";
}

private async Task<string> DispatchOnboardStart(
    long chatId,
    int messageId,
    string callbackQueryId,
    CancellationToken cancellationToken)
{
    // TODO: Start onboarding flow
    await _mediator.Send(new AnswerCallbackCommand(
        callbackQueryId, 
        "Mari mulai! 🚀"), cancellationToken);
    return "OnboardStart";
}

private async Task<string> DispatchOnboardLearn(
    long chatId,
    int messageId,
    string callbackQueryId,
    CancellationToken cancellationToken)
{
    // Show help/tutorial
    await _mediator.Send(new AnswerCallbackCommand(callbackQueryId), cancellationToken);
    await _mediator.Send(new HelpCommand(chatId), cancellationToken);
    return "OnboardLearn";
}

private async Task<string> DispatchConfirmAction(
    long chatId,
    int messageId,
    string callbackQueryId,
    string action,
    string? param,
    CancellationToken cancellationToken)
{
    // TODO: Handle confirm/reject for receipt scanning (Week 9)
    await _mediator.Send(new AnswerCallbackCommand(callbackQueryId), cancellationToken);
    return $"Confirm:{action}";
}

private async Task<string> HandleUnknownCallback(
    string callbackQueryId,
    CancellationToken cancellationToken)
{
    await _mediator.Send(new AnswerCallbackCommand(
        callbackQueryId, 
        "❓ Aksi tidak dikenali"), cancellationToken);
    return "UnknownCallback";
}
```

### Answer Callback Command

Create `src/Nastart.Api/Features/Bot/Commands/AnswerCallbackCommand.cs`:

```csharp
using MediatR;

namespace Nastart.Api.Features.Bot;

// ══════════════════════════════════════════════════════════════
// ANSWER CALLBACK COMMAND
// Acknowledges button clicks
// ══════════════════════════════════════════════════════════════

/// <summary>
/// Command to answer a callback query.
/// </summary>
public sealed record AnswerCallbackCommand(
    string CallbackQueryId,
    string? Text = null,
    bool ShowAlert = false
) : IRequest<Unit>;

/// <summary>
/// Handler to answer callback queries.
/// </summary>
public sealed class AnswerCallbackHandler : IRequestHandler<AnswerCallbackCommand, Unit>
{
    private readonly ITelegramBotService _botService;

    public AnswerCallbackHandler(ITelegramBotService botService)
    {
        _botService = botService;
    }

    public async Task<Unit> Handle(
        AnswerCallbackCommand request,
        CancellationToken cancellationToken)
    {
        await _botService.AnswerCallbackQueryAsync(
            request.CallbackQueryId,
            request.Text,
            request.ShowAlert,
            cancellationToken);
        
        return Unit.Value;
    }
}
```

### Command Router Service

Create `src/Nastart.Api/Features/Bot/Services/CommandRouter.cs`:

```csharp
namespace Nastart.Api.Features.Bot.Services;

/// <summary>
/// Routes commands to appropriate handlers.
/// </summary>
/// <remarks>
/// Centralizes command routing logic for better maintainability.
/// </remarks>
public interface ICommandRouter
{
    /// <summary>
    /// Gets handler info for a command.
    /// </summary>
    CommandInfo? GetCommandInfo(string commandName);
    
    /// <summary>
    /// Gets all available commands.
    /// </summary>
    IReadOnlyList<CommandInfo> GetAllCommands();
}

/// <summary>
/// Information about a bot command.
/// </summary>
public sealed record CommandInfo(
    string Name,
    string Description,
    string Usage,
    bool RequiresArgs = false,
    bool AdminOnly = false
);

/// <summary>
/// Implementation of command router.
/// </summary>
public class CommandRouter : ICommandRouter
{
    private static readonly Dictionary<string, CommandInfo> Commands = new()
    {
        ["start"] = new CommandInfo(
            Name: "start",
            Description: "Mulai menggunakan Nastart",
            Usage: "/start",
            RequiresArgs: false),
        
        ["help"] = new CommandInfo(
            Name: "help",
            Description: "Tampilkan bantuan",
            Usage: "/help",
            RequiresArgs: false),
        
        ["cost"] = new CommandInfo(
            Name: "cost",
            Description: "Hitung biaya resep",
            Usage: "/cost [nama resep]",
            RequiresArgs: true),
        
        ["price"] = new CommandInfo(
            Name: "price",
            Description: "Cek harga bahan",
            Usage: "/price [nama bahan]",
            RequiresArgs: true),
        
        ["low"] = new CommandInfo(
            Name: "low",
            Description: "Lihat bahan stok menipis",
            Usage: "/low",
            RequiresArgs: false),
        
        ["profit"] = new CommandInfo(
            Name: "profit",
            Description: "Ringkasan profit",
            Usage: "/profit",
            RequiresArgs: false),
        
        ["scan"] = new CommandInfo(
            Name: "scan",
            Description: "Scan struk (kirim foto)",
            Usage: "Kirim foto struk",
            RequiresArgs: false),
    };

    public CommandInfo? GetCommandInfo(string commandName)
    {
        return Commands.TryGetValue(commandName.ToLowerInvariant(), out var info) 
            ? info 
            : null;
    }

    public IReadOnlyList<CommandInfo> GetAllCommands()
    {
        return Commands.Values.ToList();
    }
}
```

### Register Services

```csharp
// In Program.cs, add:
using Nastart.Api.Features.Bot.Services;

builder.Services.AddSingleton<ICommandRouter, CommandRouter>();
```

### Your Task (Day 6):

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api

# Create Services folder
mkdir Features\Bot\Services -ErrorAction SilentlyContinue

# Create router and utility files
New-Item Features\Bot\Commands\AnswerCallbackCommand.cs
New-Item Features\Bot\Services\CommandRouter.cs

# Update HandleWebhook.cs with callback routing

# Verify build
dotnet build
```

---

# Day 7: Testing Bot Features

## 🧒 Explain Like I'm 5

Before you show your robot to friends, you need to make sure it works!
- "Does it say hello when I press start?" ✅
- "Does it show help when I ask?" ✅
- "Does it crash when I send weird stuff?" ❌ (we fix that!)

## 🔧 Engineer Language

**Integration testing** validates that bot features work correctly together. We test command handlers, webhook processing, and callback routing.

> 📖 **Microsoft Docs**: *"Integration tests ensure that an app's components function correctly at a level that includes infrastructure components."*
>
> — [Integration tests in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)

### Bot Feature Tests

Create `tests/Nastart.Api.Tests/Features/Bot/StartCommandTests.cs`:

```csharp
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Nastart.Api.Features.Bot;

namespace Nastart.Api.Tests.Features.Bot;

public class StartCommandTests
{
    private readonly Mock<ITelegramBotService> _mockBotService;
    private readonly Mock<ILogger<StartCommandHandler>> _mockLogger;
    private readonly StartCommandHandler _handler;

    public StartCommandTests()
    {
        _mockBotService = new Mock<ITelegramBotService>();
        _mockLogger = new Mock<ILogger<StartCommandHandler>>();
        
        // Note: In real tests, use in-memory database
        _handler = new StartCommandHandler(
            _mockBotService.Object,
            null!, // Mock DbContext
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_NewUser_SendsWelcomeMessage()
    {
        // Arrange
        var command = new StartCommand(
            ChatId: 123456789,
            User: new TelegramUser
            {
                Id = 987654321,
                FirstName = "Ibu",
                LastName = "Sari",
                Username = "ibusari"
            });
        
        _mockBotService
            .Setup(s => s.SendTextMessageAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.IsAny<Telegram.Bot.Types.ReplyMarkups.IReplyMarkup?>(),
                It.IsAny<Telegram.Bot.Types.Enums.ParseMode>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Telegram.Bot.Types.Message());
        
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);
        
        // Assert
        result.Should().Be(Unit.Value);
        
        _mockBotService.Verify(
            s => s.SendTextMessageAsync(
                command.ChatId,
                It.Is<string>(text => text.Contains("Nastart")),
                It.IsAny<Telegram.Bot.Types.ReplyMarkups.IReplyMarkup?>(),
                It.IsAny<Telegram.Bot.Types.Enums.ParseMode>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
```

### Webhook Handler Tests

Create `tests/Nastart.Api.Tests/Features/Bot/HandleWebhookTests.cs`:

```csharp
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Nastart.Api.Features.Bot;
using Nastart.Api.Shared.Models;

namespace Nastart.Api.Tests.Features.Bot;

public class HandleWebhookTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ILogger<HandleWebhookHandler>> _mockLogger;
    private readonly HandleWebhookHandler _handler;

    public HandleWebhookTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockLogger = new Mock<ILogger<HandleWebhookHandler>>();
        
        _handler = new HandleWebhookHandler(
            _mockMediator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_StartCommand_DispatchesToStartHandler()
    {
        // Arrange
        var update = new TelegramUpdate
        {
            UpdateId = 123,
            Message = new TelegramMessage
            {
                MessageId = 1,
                Chat = new TelegramChat { Id = 456 },
                From = new TelegramUser { Id = 789, FirstName = "Test" },
                Text = "/start"
            }
        };
        
        _mockMediator
            .Setup(m => m.Send(It.IsAny<StartCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);
        
        // Act
        var result = await _handler.Handle(
            new HandleWebhookCommand(update), 
            CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.HandlerUsed.Should().Be("StartCommand");
        
        _mockMediator.Verify(
            m => m.Send(
                It.Is<StartCommand>(c => c.ChatId == 456),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_HelpCommand_DispatchesToHelpHandler()
    {
        // Arrange
        var update = new TelegramUpdate
        {
            UpdateId = 124,
            Message = new TelegramMessage
            {
                MessageId = 2,
                Chat = new TelegramChat { Id = 456 },
                Text = "/help"
            }
        };
        
        _mockMediator
            .Setup(m => m.Send(It.IsAny<HelpCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);
        
        // Act
        var result = await _handler.Handle(
            new HandleWebhookCommand(update), 
            CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.HandlerUsed.Should().Be("HelpCommand");
    }

    [Fact]
    public async Task Handle_PhotoMessage_ReturnsPhotoReceived()
    {
        // Arrange
        var update = new TelegramUpdate
        {
            UpdateId = 125,
            Message = new TelegramMessage
            {
                MessageId = 3,
                Chat = new TelegramChat { Id = 456 },
                Photo = new[]
                {
                    new PhotoSize { FileId = "abc123", Width = 100, Height = 100 }
                }
            }
        };
        
        // Act
        var result = await _handler.Handle(
            new HandleWebhookCommand(update), 
            CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.HandlerUsed.Should().Be("PhotoReceived");
    }

    [Fact]
    public async Task Handle_UnknownCommand_DispatchesToHelpHandler()
    {
        // Arrange
        var update = new TelegramUpdate
        {
            UpdateId = 126,
            Message = new TelegramMessage
            {
                MessageId = 4,
                Chat = new TelegramChat { Id = 456 },
                Text = "/unknowncommand"
            }
        };
        
        _mockMediator
            .Setup(m => m.Send(It.IsAny<HelpCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);
        
        // Act
        var result = await _handler.Handle(
            new HandleWebhookCommand(update), 
            CancellationToken.None);
        
        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.HandlerUsed.Should().Be("UnknownCommand");
    }
}
```

### Manual Testing with ngrok

For local development, use ngrok to expose your local server:

```powershell
# Install ngrok
winget install ngrok

# Start your API
cd C:\Users\AU1833\Documents\personal\nastart\backend\src\Nastart.Api
dotnet run

# In another terminal, start ngrok
ngrok http 5000

# Copy the HTTPS URL (e.g., https://abc123.ngrok.io)
# Update appsettings.Development.json with this URL
# Call POST /api/bot/setup-webhook to register

# Test your bot in Telegram!
```

### Run Tests

```powershell
cd C:\Users\AU1833\Documents\personal\nastart\backend

# Run all bot tests
dotnet test --filter "FullyQualifiedName~Bot"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage" --filter "FullyQualifiedName~Bot"
```

### Your Task (Day 7):

1. Create `StartCommandTests.cs`
2. Create `HandleWebhookTests.cs`
3. Run tests: `dotnet test`
4. Set up ngrok for local testing
5. Test bot commands in Telegram

---

# Resources

## Microsoft Official Documentation

| Topic | Link |
|-------|------|
| **Minimal APIs** | [learn.microsoft.com/aspnet/core/fundamentals/minimal-apis](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/overview) |
| **Webhooks** | [learn.microsoft.com/aspnet/webhooks](https://learn.microsoft.com/en-us/aspnet/webhooks/) |
| **HTTP Client Factory** | [learn.microsoft.com/dotnet/core/extensions/httpclient-factory](https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-factory) |
| **Integration Tests** | [learn.microsoft.com/aspnet/core/test/integration-tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests) |
| **Options Pattern** | [learn.microsoft.com/dotnet/core/extensions/options](https://learn.microsoft.com/en-us/dotnet/core/extensions/options) |

## Telegram Documentation

| Topic | Link |
|-------|------|
| **Bot API** | [core.telegram.org/bots/api](https://core.telegram.org/bots/api) |
| **BotFather** | [core.telegram.org/bots#botfather](https://core.telegram.org/bots#botfather) |
| **Webhooks** | [core.telegram.org/bots/webhooks](https://core.telegram.org/bots/webhooks) |
| **Commands** | [core.telegram.org/bots/features#commands](https://core.telegram.org/bots/features#commands) |
| **Inline Keyboards** | [core.telegram.org/bots/features#inline-keyboards](https://core.telegram.org/bots/features#inline-keyboards) |

## External Resources

| Topic | Link |
|-------|------|
| **Telegram.Bot NuGet** | [github.com/TelegramBots/Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot) |
| **ngrok** | [ngrok.com](https://ngrok.com/) |
| **MediatR** | [github.com/jbogard/MediatR](https://github.com/jbogard/MediatR) |

## Week 8 Checklist

- [ ] Created Telegram bot via BotFather
- [ ] Saved bot token securely in appsettings
- [ ] Created `TelegramOptions` configuration class
- [ ] Installed `Telegram.Bot` NuGet package
- [ ] Created `TelegramModels.cs` with update types
- [ ] Created `TelegramBotService` wrapper
- [ ] Created `HandleWebhook` feature slice
- [ ] Created `BotEndpoints.cs` with webhook endpoint
- [ ] Registered bot services in Program.cs
- [ ] Created `StartCommand` feature slice
- [ ] Created `User` entity model
- [ ] Created `HelpCommand` feature slice
- [ ] Created `HelpCallbacks` for button clicks
- [ ] Created `AnswerCallbackCommand` utility
- [ ] Created `CommandRouter` service
- [ ] Updated webhook handler with callback routing
- [ ] Created unit tests for bot features
- [ ] Set up ngrok for local testing
- [ ] Tested `/start` and `/help` commands
- [ ] All builds pass: `dotnet build`
- [ ] All tests pass: `dotnet test`

---

## What's Next?

**Week 9: Receipt Photo Handling** — Implement photo handler to receive receipt images, download from Telegram servers, connect to OCR service, build confirmation UI with inline keyboards, and handle user confirmation/edit callbacks.

---

*Nastart — Start smart, bake profitable*
