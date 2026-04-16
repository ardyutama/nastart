namespace Nastart.Domain.Entities;

using Nastart.Domain.Common;
using Nastart.Domain.Enums;

public class TelegramLink : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public TelegramLinkStatus Status { get; set; } = TelegramLinkStatus.Pending;
    public long? TelegramUserId { get; set; }
    public string? TelegramUsername { get; set; }
    public DateTimeOffset? LinkedAt { get; set; }
}