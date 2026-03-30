using Nastart.Api.Features.Purchases;
using Nastart.Api.Features.Recipes;
using Nastart.Api.Features.Shops;

namespace Nastart.Api.Features.Users;

public class User
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public string? BusinessName { get; set; }
    public string? BusinessType { get; set; } = "Bakery";
    public decimal? MinMarginPercent { get; set; }
    public long? TelegramId { get; set; }
    public string? TelegramUsername { get; set; }
    public bool IsOnboarded { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<Recipe> Recipes { get; set; } = [];
    public ICollection<Purchase> Purchases { get; set; } = [];
    public ICollection<Shop> Shops { get; set; } = [];
}